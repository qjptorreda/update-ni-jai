using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RescuAR.App.Models;
using RescuAR.Services;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;

namespace RescuAR.App.Services.Cloud
{
    public class SafetyCircleService
    {
        private Supabase.Client GetClient()
        {
            var client = SupabaseService.Instance.Client;
            if (client == null)
            {
                throw new InvalidOperationException("Supabase client is not initialized.");
            }
            return client;
        }

        public string GetCurrentUserId()
        {
            var client = GetClient();
            return client.Auth.CurrentUser?.Id ?? throw new UnauthorizedAccessException("User is not logged in.");
        }

        // --- Circle Management ---

        public async Task<SupabaseSafetyCircle> CreateCircleAsync(string name)
        {
            var client = GetClient();
            var userId = GetCurrentUserId();

            // Generate a random 6-character invite code
            string inviteCode = GenerateInviteCode();

            var newCircle = new SupabaseSafetyCircle
            {
                Name = name,
                InviteCode = inviteCode,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            var response = await client.From<SupabaseSafetyCircle>().Insert(newCircle);
            var circle = response.Models.FirstOrDefault();

            if (circle != null)
            {
                // Auto-join the creator
                await JoinCircleInternalAsync(circle.Id, userId);
            }

            return circle ?? throw new Exception("Failed to create circle.");
        }

        public async Task<SupabaseSafetyCircle> JoinCircleWithCodeAsync(string inviteCode)
        {
            var client = GetClient();
            var userId = GetCurrentUserId();

            // Find circle by code
            var circleResponse = await client.From<SupabaseSafetyCircle>()
                .Where(x => x.InviteCode == inviteCode.ToUpper())
                .Get();

            var circle = circleResponse.Models.FirstOrDefault();
            if (circle == null)
            {
                throw new Exception("Invalid invite code.");
            }

            await JoinCircleInternalAsync(circle.Id, userId);
            return circle;
        }

        private async Task JoinCircleInternalAsync(string circleId, string userId)
        {
            var client = GetClient();

            // Check if already a member
            var existing = await client.From<SupabaseCircleMember>()
                .Where(x => x.CircleId == circleId && x.UserId == userId)
                .Get();

            if (existing.Models.Any()) return; // Already joined

            var member = new SupabaseCircleMember
            {
                CircleId = circleId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };

            await client.From<SupabaseCircleMember>().Insert(member);
        }

        public async Task<List<SupabaseSafetyCircle>> GetMyCirclesAsync()
        {
            var client = GetClient();
            var userId = GetCurrentUserId();

            // Fetch circle memberships for current user
            var membershipsResponse = await client.From<SupabaseCircleMember>()
                .Select("circle_id")
                .Where(x => x.UserId == userId)
                .Get();

            var circleIds = membershipsResponse.Models.Select(m => m.CircleId).ToList();
            if (!circleIds.Any())
            {
                return new List<SupabaseSafetyCircle>();
            }

            // Fetch the actual circle details
            var circlesResponse = await client.From<SupabaseSafetyCircle>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.In, circleIds)
                .Get();

            return circlesResponse.Models;
        }

        public async Task<List<User>> GetCircleMembersAsync(string circleId)
        {
            var client = GetClient();

            // Fetch member user IDs
            var membershipsResponse = await client.From<SupabaseCircleMember>()
                .Select("user_id")
                .Where(x => x.CircleId == circleId)
                .Get();

            var userIds = membershipsResponse.Models.Select(m => m.UserId).ToList();
            if (!userIds.Any()) return new List<User>();

            // Fetch full users containing AvatarUrl
            var usersResponse = await client.From<User>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.In, userIds)
                .Get();

            return usersResponse.Models;
        }

        // --- Location Tracking ---

        public async Task PushLocationAsync(double lat, double lon, string status = "In Transit")
        {
            try
            {
                var client = GetClient();
                var userId = GetCurrentUserId();

                var location = new SupabaseUserLocation
                {
                    UserId = userId,
                    Latitude = lat,
                    Longitude = lon,
                    StatusText = status,
                    LastUpdated = DateTime.UtcNow
                };

                await client.From<SupabaseUserLocation>().Upsert(location);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to push location: {ex.Message}");
            }
        }

        public async Task<List<SupabaseUserLocation>> GetCircleLocationsAsync(string circleId)
        {
            var client = GetClient();

            // Get user ids in circle
            var membershipsResponse = await client.From<SupabaseCircleMember>()
                .Select("user_id")
                .Where(x => x.CircleId == circleId)
                .Get();

            var userIds = membershipsResponse.Models.Select(m => m.UserId).ToList();
            if (!userIds.Any()) return new List<SupabaseUserLocation>();

            // Fetch latest locations
            var locationsResponse = await client.From<SupabaseUserLocation>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.In, userIds)
                .Get();

            return locationsResponse.Models;
        }

        // --- Chat Messaging ---
        private static readonly Dictionary<string, List<SupabaseCircleMessage>> _inMemoryMessages = new();

        public async Task<SupabaseCircleMessage?> SendMessageAsync(string circleId, string messageText, string? mediaUrl = null, string? mediaType = "Text")
        {
            string senderName = "Family Member";
            string avatarUrl = string.Empty;
            string userId = string.Empty;

            try
            {
                var client = GetClient();
                userId = GetCurrentUserId();

                try
                {
                    var userResp = await client.From<User>().Where(u => u.Id == userId).Get();
                    var user = userResp.Models.FirstOrDefault();
                    if (user != null)
                    {
                        senderName = $"{user.FirstName} {user.LastName}".Trim();
                        if (string.IsNullOrWhiteSpace(senderName)) senderName = user.Username;
                        avatarUrl = user.AvatarUrl;
                    }
                }
                catch { }
            }
            catch { }

            var msg = new SupabaseCircleMessage
            {
                Id = Guid.NewGuid().ToString(),
                CircleId = circleId,
                UserId = userId,
                SenderName = senderName,
                SenderAvatarUrl = avatarUrl,
                MessageText = messageText ?? string.Empty,
                MediaUrl = mediaUrl ?? string.Empty,
                MediaType = string.IsNullOrWhiteSpace(mediaUrl) ? "Text" : (mediaType ?? "Image"),
                CreatedAt = DateTime.UtcNow
            };

            // Always store in memory cache
            lock (_inMemoryMessages)
            {
                if (!_inMemoryMessages.ContainsKey(circleId))
                    _inMemoryMessages[circleId] = new List<SupabaseCircleMessage>();
                _inMemoryMessages[circleId].Add(msg);
            }

            try
            {
                var client = GetClient();
                var insertResp = await client.From<SupabaseCircleMessage>().Insert(msg);
                return insertResp.Models.FirstOrDefault() ?? msg;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase send message error (using local memory store): {ex.Message}");
                return msg;
            }
        }

        public async Task<List<SupabaseCircleMessage>> GetCircleMessagesAsync(string circleId)
        {
            try
            {
                var client = GetClient();
                var resp = await client.From<SupabaseCircleMessage>()
                    .Where(m => m.CircleId == circleId)
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                if (resp.Models != null && resp.Models.Count > 0)
                {
                    return resp.Models;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCircleMessages remote error: {ex.Message}");
            }

            // Return in-memory fallback if remote table is empty or pending
            lock (_inMemoryMessages)
            {
                if (_inMemoryMessages.TryGetValue(circleId, out var list))
                {
                    return list.OrderBy(m => m.CreatedAt).ToList();
                }
            }

            return new List<SupabaseCircleMessage>();
        }

        private string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
