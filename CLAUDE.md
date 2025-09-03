# Claude Code Instructions

## Development Workflow Expectations

**IMPORTANT:** Always commit changes after completing tasks or fixing issues. When a task is complete:
1. Stage relevant files with `git add`
2. Create a descriptive commit message following the project's commit style
3. Include the standard Claude Code footer in all commits
4. Verify the commit succeeded with `git status`

## Project Overview
This is a real-time auction draft web application for fantasy sports, specifically designed for the NFL Draft League. The system allows multiple users to participate in live auctions bidding on college schools, with real-time updates and comprehensive roster management.

## Architecture Summary
- **Frontend:** Blazor WebAssembly (WASM) for client-side UI and logic
- **UI Framework:** Microsoft Fluent UI Blazor components for professional business interface
- **Real-time:** Azure SignalR Service for live bidding updates  
- **Backend:** Azure Functions (within Static Web Apps) for API endpoints
- **Database:** Azure SQL Database for persistent data storage
- **Hosting:** Azure Static Web Apps for deployment

## Key Documentation Files

### Core Design Documents
- **README-Design.md** - Original design requirements and business rules
- **PRODUCT-DESIGN.md** - Comprehensive technical architecture and user flows
- **DATABASE-ERD.md** - Complete database schema with 12 entities and relationships
- **DEVELOPMENT-TASKS.md** - 6-phase implementation plan with 23 specific tasks

### Sample Data Files
- **SampleDraftTemplate.csv** - Input CSV format with school data and statistics
- **SampleFantasyDraft.csv** - Output CSV format showing auction results

## Critical Design Decisions

### Single Environment Strategy
- **All testing occurs on deployed Azure infrastructure** - cannot simulate multi-user SignalR locally
- Development and production use same environment initially
- Database designed for multi-tenancy from day 1
- Feature flags for incomplete functionality during development

### Authentication Model
- **No traditional login system** - users join via auction-specific codes
- Users enter join code + display name (case-insensitive)
- Auction Master assigns roles after users join
- Reconnection requires Auction Master approval
- State synchronization on reconnect with loading screen

### School Management System
- **Persistent school database** separate from auction-specific data
- SchoolId, Name, SchoolURL (logo) stored once, referenced by multiple auctions
- CSV import matches school names with fuzzy matching + confirmation
- Auction Master confirms all fuzzy matches before proceeding
- New schools created automatically for unrecognized names

## Implementation Priority Order

1. **School Management** - Core school database with logo URLs and admin interface
2. **Auction Creation** - CSV upload, school matching, roster design, validation
3. **Join Auction** - User authentication, role assignment, reconnection system  
4. **Real-time Bidding** - SignalR hub, nomination system, bidding, results export

## Key Business Rules

### Auction Flow
- **Nomination order** set by Auction Master before auction starts
- **Auto-bidding** - nominating user automatically bids $1
- **Manual passing** - no timers, users click "Pass" button or Auction Master ends bidding
- **Budget validation** - MaxBid = Budget - (EmptyRosterSlots - 1)
- **Roster assignment** - auto-assign to most restrictive valid position, user can override

### Role-Based Permissions
- **Auction Master** - full control, can override/fix any issues during live auction
- **Team Coach** - can nominate schools and bid when it's their turn
- **Proxy Coach** - can bid for multiple teams, dropdown to switch active team
- **Auction Viewer** - read-only access to auction progress

### Strategic Elements
- Users can make poor positioning decisions (SEC school in Flex slot) - this is strategy, not a bug
- System prevents impossible auctions (more roster slots than available schools)
- Replacement value calculations depend on team count and roster structure

## Admin Features

### System Admin Interface
- Access via `/admin` with master password
- **Auction Management** - list all auctions with status, participants, last activity
- **Bulk Operations** - delete test auctions, archive completed auctions
- **Manual Control** - preferred over automated cleanup
- **School Management** - add/edit school logos and basic information

### Auction Master Admin Panel
- **Separate tab/window** with live connection status of all participants
- **Role Overview** - clear mapping of proxy coaches to their teams
- **Reconnection Approval** - queue of users requesting to rejoin
- **Override Controls** - end bidding, assign schools to roster positions
- **Source of Truth** - what Auction Master sees resolves disputes (video call context)

## Development Guidelines

### Testing Strategy
- **Deploy early and often** - any real-time feature requires live deployment
- **Multi-user testing** - use multiple browser windows/devices on deployed environment
- **No local SignalR testing** - impossible to simulate real-time multi-user scenarios
- **Feature flags** for incomplete functionality during development

### Code Conventions

#### Blazor & .NET Standards
- Use C# 13 features and .NET 8+ optimizations
- Follow PascalCase for components, methods, public members; camelCase for private fields
- Prefix interfaces with "I" (e.g., IUserService)
- Use primary constructor syntax for dependency injection: `public class MyClass(IDependency dependency)`
- Implement comprehensive XML documentation for all public APIs

#### Component Architecture  
- Utilize Blazor component lifecycle methods (OnInitializedAsync, OnParametersSetAsync)
- Use @bind for data binding, EventCallbacks for user interactions
- Minimize renders with ShouldRender() and efficient StateHasChanged() usage
- Implement ErrorBoundary for UI-level error handling
- Separate complex logic into code-behind or service classes

#### UI Framework Standards
- Use **Microsoft Fluent UI Blazor** components for all UI elements
- Install templates: `dotnet new install Microsoft.FluentUI.AspNetCore.Templates`
- Leverage FluentDataGrid for real-time auction tables and admin interfaces
- Use FluentDialog, FluentTextField, FluentSelect for consistent user interactions
- Follow Fluent Design principles for professional business application appearance

#### State Management & Performance
- Use Cascading Parameters and EventCallbacks for basic state sharing
- Implement IMemoryCache for server-side caching
- Use localStorage/sessionStorage for client-side state persistence
- Optimize component render tree to avoid unnecessary re-renders

#### Error Handling & Validation
- Use structured logging with Microsoft.Extensions.Logging
- Implement FluentValidation or DataAnnotations for forms
- Use async/await with ConfigureAwait(false) for I/O operations
- Handle async exceptions properly with try-catch blocks

#### Testing Requirements
- Unit tests with xUnit/MSTest using AAA pattern (Arrange, Act, Assert)
- Use Moq or NSubstitute for mocking dependencies  
- FluentAssertions for readable test assertions
- Test both success and failure scenarios including null validation

#### Security & Dependencies
- Server-side validation for all critical business logic
- Check for library availability before using (look at project files, neighboring code)
- Never commit secrets or keys to repository
- Use HTTPS and proper CORS policies
- Implement parameterized queries for database operations

### Deployment Approach
- **Azure Static Web Apps** with CI/CD from repository
- **Single environment** serves both development and production initially
- **Connection strings** configured for production-grade services
- **Quick iteration** with small, frequent deployments

## SignalR Architecture

### Connection Groups
- `auction-{auctionId}` - regular participants receive auction updates
- `admin-{auctionId}` - Auction Master receives additional admin-only broadcasts

### Key Hub Methods (Planned)
- `PlaceBid(schoolId, amount)` - place a bid on nominated school
- `NominateSchool(schoolId)` - nominate school for bidding (auto-bids $1)
- `PassOnSchool()` - pass on current school being bid on
- `ApproveReconnection(userId)` - Auction Master approves user rejoining
- `EndCurrentBid()` - Auction Master manually ends current bidding
- `AssignSchoolToPosition(teamId, schoolId, positionId)` - assign won school

### Broadcast Events
- New bid placed with amount, bidder, current high bid
- School won with winner and final price
- User connection/disconnection status changes
- Nomination turn changes and roster updates

## Data Validation

### CSV Import Validation
- **Required columns** - School, Conference, ProjectedPoints, LeagifyPosition, etc.
- **School matching** - fuzzy match with confirmation for uncertain matches
- **Data types** - validate numeric fields, URL formats for school logos
- **Error handling** - graceful failure with clear user feedback

### Auction Setup Validation  
- **Roster validation** - prevent more position slots than available schools
- **Budget validation** - ensure teams have sufficient budget for roster completion
- **Turn order** - validate nomination order includes all active team coaches

### Real-time Bid Validation
- **Budget constraints** - prevent bids that would leave insufficient funds for remaining slots
- **Roster eligibility** - only users with open roster slots can nominate
- **Position validity** - ensure school can be assigned to available roster positions

## Error Handling Strategy

### Network Issues
- **Reconnection approval** by Auction Master prevents impersonation
- **State synchronization** ensures rejoining users get current auction state
- **Auction Master override** can resolve any stuck situations during live auction

### Business Logic Errors
- **Server-side validation** prevents all invalid operations
- **Clear error messages** explain why actions failed
- **Admin controls** allow Auction Master to fix problems during live events

## Important Notes

### User Experience Context
- **Video call assumption** - users participate via video call with shared screen
- **Desktop focus** - mobile support is future enhancement
- **Auction Master authority** - their view is source of truth for disputes
- **Strategic gameplay** - allow users to make suboptimal decisions

### Data Management
- **Audit trail** for winning bids (not intermediate bids)
- **Multi-auction support** - system supports multiple concurrent auctions
- **Cleanup commands** - manual admin control for removing test data
- **Export format** - matches SampleFantasyDraft.csv structure exactly

## Development Commands

When implementing this system:

1. **UI Framework setup** - Install Microsoft Fluent UI templates: `dotnet new install Microsoft.FluentUI.AspNetCore.Templates`
2. **Run tests and validation** - Use `dotnet test`, `dotnet build --verify-no-warnings`, and any configured lint/format commands before committing
3. **Code quality checks** - Ensure all public APIs have XML documentation, follow SOLID principles
4. **Performance testing** - Profile component renders and optimize using Visual Studio diagnostics tools  
5. **Deploy frequently** - Push to Azure Static Web Apps for any real-time feature testing  
6. **Multi-user testing** - Open multiple browser windows/tabs to test auction scenarios
7. **Admin interface testing** - Verify admin controls work correctly in deployed environment

## Next Steps

Ready to begin implementation with **Phase 1: Foundation & School Management**:
- Project setup and Azure infrastructure
- School entity management system  
- System admin authentication
- Basic deployment pipeline

Refer to DEVELOPMENT-TASKS.md for detailed task breakdown and success criteria.