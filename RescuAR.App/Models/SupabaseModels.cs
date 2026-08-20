using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RescuAR.App.Models
{
    [Table("safety_circles")]
    public class SupabaseSafetyCircle : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("invite_code")]
        public string InviteCode { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("created_by")]
        public string CreatedBy { get; set; } = string.Empty;
    }

    [Table("safety_circle_members")]
    public class SupabaseCircleMember : BaseModel
    {
        [PrimaryKey("circle_id", true)]
        public string CircleId { get; set; } = string.Empty;

        [PrimaryKey("user_id", true)]
        public string UserId { get; set; } = string.Empty;

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }
    }

    [Table("user_locations")]
    public class SupabaseUserLocation : BaseModel
    {
        [PrimaryKey("user_id", true)]
        public string UserId { get; set; } = string.Empty;

        [Column("latitude")]
        public double Latitude { get; set; }

        [Column("longitude")]
        public double Longitude { get; set; }

        [Column("status_text")]
        public string StatusText { get; set; } = string.Empty;

        [Column("last_updated")]
        public DateTime LastUpdated { get; set; }
    }

    [Table("profiles")]
    public class SupabaseProfile : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("initials")]
        public string Initials { get; set; } = string.Empty;
    }
}
