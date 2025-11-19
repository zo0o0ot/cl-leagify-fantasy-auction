using Microsoft.FluentUI.AspNetCore.Components;

namespace LeagifyFantasyAuction.Services;

/// <summary>
/// Service for displaying toast notifications throughout the application.
/// Handles reconnection notifications, auction events, and general user alerts.
/// </summary>
public class NotificationService
{
    private readonly IToastService _toastService;

    public NotificationService(IToastService toastService)
    {
        _toastService = toastService;
    }

    /// <summary>
    /// Shows a reconnection approved notification.
    /// </summary>
    /// <param name="approvedBy">Name of the user who approved the reconnection.</param>
    public void ShowReconnectionApproved(string approvedBy)
    {
        _toastService.ShowSuccess(
            $"Reconnection approved by {approvedBy}",
            timeout: 5000
        );
    }

    /// <summary>
    /// Shows a reconnection denied notification.
    /// </summary>
    /// <param name="reason">Optional reason for denial.</param>
    public void ShowReconnectionDenied(string? reason = null)
    {
        var message = string.IsNullOrEmpty(reason)
            ? "Your reconnection request was denied"
            : $"Reconnection denied: {reason}";

        _toastService.ShowError(message, timeout: 8000);
    }

    /// <summary>
    /// Shows a notification that reconnection request was sent.
    /// </summary>
    public void ShowReconnectionRequested()
    {
        _toastService.ShowInfo(
            "Reconnection request sent to Auction Master. Please wait for approval...",
            timeout: 5000
        );
    }

    /// <summary>
    /// Shows a notification when a new user joins the auction.
    /// </summary>
    /// <param name="displayName">Display name of the user who joined.</param>
    public void ShowUserJoined(string displayName)
    {
        _toastService.ShowInfo(
            $"{displayName} joined the auction",
            timeout: 3000
        );
    }

    /// <summary>
    /// Shows a notification when a user leaves the auction.
    /// </summary>
    /// <param name="displayName">Display name of the user who left.</param>
    public void ShowUserLeft(string displayName)
    {
        _toastService.ShowWarning(
            $"{displayName} left the auction",
            timeout: 3000
        );
    }

    /// <summary>
    /// Shows a notification when a user's role is assigned or updated.
    /// </summary>
    /// <param name="role">The role that was assigned.</param>
    /// <param name="teamName">Optional team name for coach roles.</param>
    public void ShowRoleAssigned(string role, string? teamName = null)
    {
        var roleDisplay = role switch
        {
            "AuctionMaster" => "Auction Master",
            "TeamCoach" => "Team Coach",
            "ProxyCoach" => "Proxy Coach",
            "Viewer" => "Viewer",
            _ => role
        };

        var message = string.IsNullOrEmpty(teamName)
            ? $"You have been assigned as {roleDisplay}"
            : $"You have been assigned as {roleDisplay} for {teamName}";

        _toastService.ShowSuccess(message, timeout: 5000);
    }

    /// <summary>
    /// Shows a notification when bidding starts on a school.
    /// </summary>
    /// <param name="schoolName">Name of the school being bid on.</param>
    /// <param name="nominatedBy">User who nominated the school.</param>
    public void ShowBiddingStarted(string schoolName, string nominatedBy)
    {
        _toastService.ShowInfo(
            $"Bidding started on {schoolName} (nominated by {nominatedBy})",
            timeout: 4000
        );
    }

    /// <summary>
    /// Shows a notification when bidding ends.
    /// </summary>
    /// <param name="schoolName">Name of the school.</param>
    /// <param name="winner">User who won the bid.</param>
    /// <param name="amount">Winning bid amount.</param>
    public void ShowBiddingEnded(string schoolName, string winner, decimal amount)
    {
        _toastService.ShowSuccess(
            $"{winner} won {schoolName} for ${amount}",
            timeout: 5000
        );
    }

    /// <summary>
    /// Shows a notification when auction master manually ends bidding.
    /// </summary>
    public void ShowBiddingEndedByMaster()
    {
        _toastService.ShowWarning(
            "Auction Master has ended the current bidding",
            timeout: 4000
        );
    }

    /// <summary>
    /// Shows a notification when the auction starts.
    /// </summary>
    public void ShowAuctionStarted()
    {
        _toastService.ShowSuccess(
            "The auction has started! Good luck!",
            timeout: 5000
        );
    }

    /// <summary>
    /// Shows a notification when the auction is paused.
    /// </summary>
    public void ShowAuctionPaused()
    {
        _toastService.ShowWarning(
            "The auction has been paused by the Auction Master",
            timeout: 5000
        );
    }

    /// <summary>
    /// Shows a notification when the auction is completed.
    /// </summary>
    public void ShowAuctionCompleted()
    {
        _toastService.ShowSuccess(
            "The auction is complete! Check the results.",
            timeout: 8000
        );
    }

    /// <summary>
    /// Shows a notification when it's the user's turn to nominate.
    /// </summary>
    public void ShowYourTurnToNominate()
    {
        _toastService.ShowInfo(
            "It's your turn to nominate a school!",
            timeout: 0 // Stay until dismissed
        );
    }

    /// <summary>
    /// Shows a generic success notification.
    /// </summary>
    public void ShowSuccess(string message, int timeout = 3000)
    {
        _toastService.ShowSuccess(message, timeout: timeout);
    }

    /// <summary>
    /// Shows a generic error notification.
    /// </summary>
    public void ShowError(string message, int timeout = 5000)
    {
        _toastService.ShowError(message, timeout: timeout);
    }

    /// <summary>
    /// Shows a generic info notification.
    /// </summary>
    public void ShowInfo(string message, int timeout = 3000)
    {
        _toastService.ShowInfo(message, timeout: timeout);
    }

    /// <summary>
    /// Shows a generic warning notification.
    /// </summary>
    public void ShowWarning(string message, int timeout = 4000)
    {
        _toastService.ShowWarning(message, timeout: timeout);
    }
}
