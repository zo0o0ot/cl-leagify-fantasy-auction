namespace LeagifyFantasyAuction.Models;

public class AuctionDto
{
    public int AuctionId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string JoinCode { get; set; } = "";
    public string MasterCode { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}