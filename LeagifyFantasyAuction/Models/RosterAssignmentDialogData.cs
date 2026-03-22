namespace LeagifyFantasyAuction.Models;

public class RosterAssignmentDialogData
{
    public int AuctionId { get; set; }
    public int DraftPickId { get; set; }
    public int TeamId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
}

public class RosterAssignmentResult
{
    public bool AutoAssign { get; set; }
    public int? RosterPositionId { get; set; }
}
