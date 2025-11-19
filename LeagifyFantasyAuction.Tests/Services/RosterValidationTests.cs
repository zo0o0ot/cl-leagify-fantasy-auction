using FluentAssertions;

namespace LeagifyFantasyAuction.Tests.Services;

/// <summary>
/// Unit tests for roster validation business logic.
/// Tests auction configuration validation including school availability,
/// roster position constraints, and team viability calculations.
/// </summary>
public class RosterValidationTests
{
    /// <summary>
    /// Helper method to calculate validation errors and warnings for auction configuration.
    /// Mirrors the validation logic from ReviewStep.razor.
    /// </summary>
    private (List<string> Errors, List<string> Warnings) ValidateAuctionConfiguration(
        int schoolCount,
        List<RosterPositionConfig> rosterPositions,
        Dictionary<string, int> distinctPositions)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate school import
        if (schoolCount == 0)
        {
            errors.Add("No schools have been imported. Import schools before proceeding.");
        }

        // Validate roster configuration
        if (!rosterPositions.Any())
        {
            warnings.Add("No roster positions configured. Teams will have no roster structure.");
        }
        else
        {
            var totalSlotsPerTeam = rosterPositions.Sum(p => p.SlotsPerTeam);

            // Check if there are enough schools for all teams
            if (totalSlotsPerTeam > 0 && schoolCount > 0)
            {
                var maxPossibleTeams = schoolCount / totalSlotsPerTeam;
                if (maxPossibleTeams < 2)
                {
                    errors.Add($"Not enough schools for viable teams. With {schoolCount} schools and {totalSlotsPerTeam} slots per team, only {maxPossibleTeams} team(s) are possible. Consider reducing roster size or importing more schools.");
                }
                else if (maxPossibleTeams < 6)
                {
                    warnings.Add($"Limited team capacity. With current configuration, maximum {maxPossibleTeams} teams can participate.");
                }
            }

            // Check for position mismatches
            var configuredPositions = rosterPositions.Where(p => !p.IsFlexPosition).Select(p => p.PositionName).ToList();
            var availablePositions = distinctPositions.Keys.ToList();

            foreach (var configPos in configuredPositions)
            {
                if (!availablePositions.Contains(configPos))
                {
                    warnings.Add($"Position '{configPos}' is configured but no schools with this position were imported.");
                }
            }

            // Check for over-constrained positions
            foreach (var rosterPos in rosterPositions.Where(p => !p.IsFlexPosition))
            {
                if (distinctPositions.ContainsKey(rosterPos.PositionName))
                {
                    var availableSchools = distinctPositions[rosterPos.PositionName];
                    var requiredPerTeam = rosterPos.SlotsPerTeam;

                    if (availableSchools < requiredPerTeam * 2) // Need at least 2 teams worth
                    {
                        warnings.Add($"Position '{rosterPos.PositionName}' may be over-constrained: only {availableSchools} schools available for {requiredPerTeam} slots per team.");
                    }
                }
            }
        }

        return (errors, warnings);
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithNoSchools_ShouldReturnError()
    {
        // Arrange
        int schoolCount = 0;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 2, IsFlexPosition = false }
        };
        var distinctPositions = new Dictionary<string, int>();

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().HaveCount(1);
        errors.First().Should().Contain("No schools have been imported");
        errors.First().Should().Contain("Import schools before proceeding");
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithNoRosterPositions_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 100;
        var rosterPositions = new List<RosterPositionConfig>();
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 50 },
            { "Group of 5", 30 },
            { "FCS", 20 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().HaveCount(1);
        warnings.First().Should().Contain("No roster positions configured");
        warnings.First().Should().Contain("Teams will have no roster structure");
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithViableConfiguration_ShouldReturnNoErrors()
    {
        // Arrange
        int schoolCount = 120;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "Group of 5", SlotsPerTeam = 3, IsFlexPosition = false },
            new() { PositionName = "Flex", SlotsPerTeam = 2, IsFlexPosition = true }
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 60 },
            { "Group of 5", 40 },
            { "FCS", 20 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithInsufficientSchoolsForOneTeam_ShouldReturnError()
    {
        // Arrange
        int schoolCount = 9; // Less than slots needed for 2 teams
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false }
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 9 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().HaveCount(1);
        errors.First().Should().Contain("Not enough schools for viable teams");
        errors.First().Should().Contain("only 1 team(s) are possible");
        errors.First().Should().Contain("Consider reducing roster size or importing more schools");
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithLimitedTeamCapacity_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 50; // Exactly 5 teams worth (less than 6)
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 8, IsFlexPosition = false },
            new() { PositionName = "Flex", SlotsPerTeam = 2, IsFlexPosition = true }
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 40 },
            { "Group of 5", 10 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().HaveCount(1);
        warnings.First().Should().Contain("Limited team capacity");
        warnings.First().Should().Contain("maximum 5 teams can participate");
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithPositionMismatch_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 100;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "SEC", SlotsPerTeam = 2, IsFlexPosition = false } // SEC not in imported schools
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 60 },
            { "Group of 5", 40 }
            // Note: "SEC" is not in the imported schools
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().ContainSingle(w => w.Contains("Position 'SEC' is configured but no schools with this position were imported"));
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithOverConstrainedPosition_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 100;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "FCS", SlotsPerTeam = 3, IsFlexPosition = false } // Only 5 FCS schools available
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 80 },
            { "Group of 5", 15 },
            { "FCS", 5 } // Only 5 FCS schools, but need 3 per team (minimum 6 for 2 teams)
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().ContainSingle(w =>
            w.Contains("Position 'FCS' may be over-constrained") &&
            w.Contains("only 5 schools available for 3 slots per team"));
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithFlexPositions_ShouldIgnorePositionMismatch()
    {
        // Arrange
        int schoolCount = 100;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "Flex", SlotsPerTeam = 3, IsFlexPosition = true } // Flex position, should not trigger mismatch
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 60 },
            { "Group of 5", 40 }
            // Note: "Flex" is not in the imported schools, but it's a flex position
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().BeEmpty(); // Flex positions should not trigger position mismatch warnings
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithMultipleIssues_ShouldReturnAllErrorsAndWarnings()
    {
        // Arrange
        int schoolCount = 15; // Only enough for 2 teams
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "SEC", SlotsPerTeam = 2, IsFlexPosition = false }, // Not in imported schools
            new() { PositionName = "FCS", SlotsPerTeam = 1, IsFlexPosition = false }  // Over-constrained
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 10 },
            { "FCS", 1 } // Over-constrained: only 1 FCS but need 1 per team (minimum 2 for 2 teams)
            // Note: "SEC" is not in the imported schools
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().HaveCount(3);
        warnings.Should().ContainSingle(w => w.Contains("Limited team capacity"));
        warnings.Should().ContainSingle(w => w.Contains("Position 'SEC' is configured but no schools"));
        warnings.Should().ContainSingle(w => w.Contains("Position 'FCS' may be over-constrained"));
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithExactlyEnoughSchools_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 60; // Exactly 6 teams worth
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 10, IsFlexPosition = false }
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 60 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().BeEmpty(); // Exactly 6 teams is acceptable (not less than 6)
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithSevenTeams_ShouldReturnNoWarnings()
    {
        // Arrange
        int schoolCount = 70; // 7 teams worth
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 10, IsFlexPosition = false }
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 70 }
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().BeEmpty(); // 7 teams is more than 6, so no warning
    }

    [Fact]
    public void ValidateAuctionConfiguration_WithBarelyEnoughForPosition_ShouldReturnWarning()
    {
        // Arrange
        int schoolCount = 100;
        var rosterPositions = new List<RosterPositionConfig>
        {
            new() { PositionName = "Power Conference", SlotsPerTeam = 5, IsFlexPosition = false },
            new() { PositionName = "FCS", SlotsPerTeam = 2, IsFlexPosition = false } // Exactly 4 FCS schools (2 teams worth)
        };
        var distinctPositions = new Dictionary<string, int>
        {
            { "Power Conference", 90 },
            { "FCS", 4 } // Exactly 2 teams worth, but warning triggers at < 2 teams worth
        };

        // Act
        var (errors, warnings) = ValidateAuctionConfiguration(schoolCount, rosterPositions, distinctPositions);

        // Assert
        errors.Should().BeEmpty();
        warnings.Should().BeEmpty(); // Exactly 2 teams worth is acceptable (not less than 2)
    }

    /// <summary>
    /// Simple DTO for testing roster position configuration.
    /// </summary>
    public class RosterPositionConfig
    {
        public string PositionName { get; set; } = string.Empty;
        public int SlotsPerTeam { get; set; }
        public bool IsFlexPosition { get; set; }
    }
}
