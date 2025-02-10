using System;
using System.Collections.Generic;

namespace Mewdeko.Database.Models
{
    public class UserAffiliate
    {
        public int UserId { get; set; }
        public Lifecycle? Lifecycle { get; set; }
    }

    public class Lifecycle
    {
        public DateTime? REGISTERED { get; set; }
        public DateTime? EMAIL_VERIFIED { get; set; }
        public DateTime? KYC_COMPLETED { get; set; }
        public DateTime? FIRST_DEPOSIT { get; set; }
        public DateTime? FIRST_TRADE { get; set; }
    }

    public class GuildMemberResponse
    {
        public string? Name { get; set; }
        public string? imgUrl { get; set; }
        public string? Description { get; set; }
        public DateTime? created { get; set; }
        public bool isPrivate { get; set; }
        public bool archived { get; set; }
        public string? emoji { get; set; }
        public string? logoUrl { get; set; }
        public string? mobileHeroImgUrl { get; set; }

        // socials

        public int? potAccountId { get; set; }

        // guildPotBalance

        public int? memberCount { get; set; }
        public int? memberCapacity { get; set; }
        public List<BMexMember>? Members { get; set; }
    }

    public class BMexMember
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Role { get; set; }
        public int Rank { get; set; }
        public decimal? ADV30Day { get; set; }
        public decimal? Pnl { get; set; }
        public decimal? Volume { get; set; }
        public decimal? VolumeUSDT { get; set; }
        public decimal? VolumeTaker { get; set; }
        public decimal? Roi { get; set; }
        public bool ShareTrades { get; set; }
    }

    public class ErrorResponse
    {
        public Error? Error { get; set; }
    }

    public class Error
    {
        public string? Message { get; set; }
        public string? Name { get; set; }
    }
}
