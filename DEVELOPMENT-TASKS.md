# Leagify Fantasy Auction - Development Task Breakdown

## Implementation Priority Order

Based on the requirement: **School Management → Auction Creation → Join Auction → Real-time Bidding**

---

## Phase 1: Foundation & School Management (Weeks 1-2)

### Task 1.1: Project Setup and Azure Infrastructure
**Priority:** Critical  
**Estimated Effort:** 4-6 hours  
**Dependencies:** None

- [x] Install Microsoft Fluent UI Blazor templates: `dotnet new install Microsoft.FluentUI.AspNetCore.Templates`
- [x] Create Blazor WebAssembly project with Fluent UI template
- [x] Set up Azure SQL Database with connection strings
- [x] Configure Azure SignalR Service (basic setup)
- [x] Implement basic project structure (Models, Services, Pages) following C# 13/.NET 8+ patterns
- [x] Add Microsoft.Extensions.Logging for structured logging
- [x] Create deployment workflow for Azure Static Web Apps
- [x] Verify deployed app loads correctly with Fluent UI components

**Deliverables:**
- Working Blazor app deployed to Azure
- Database connectivity established
- Basic CI/CD pipeline functional

### Task 1.2: School Entity Management System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 1.1

- [x] Create School entity and database schema
- [x] Implement School repository with CRUD operations (converted to database-backed)
- [x] Build management interface using FluentDataGrid for school management
- [x] Add external logo URL loading with fallback to internal files using FluentTextField
- [x] Create school search and fuzzy matching logic
- [x] Implement school creation from unmatched names
- [x] Restore FluentUI framework with proper MIME configuration
- [x] Convert basic HTML table to FluentDataGrid with built-in pagination (20 items per page)
- [x] Add sortable columns and professional FluentUI styling
- [x] Implement CSV import with logo download functionality
- [x] Add comprehensive XML documentation for all public school management APIs
- [x] Implement unit tests using xUnit with AAA pattern for school services
- [x] Convert to database-backed persistent storage with Azure SQL Database
- [x] Implement local-first logo serving strategy with URL fallback
- [x] Add database initialization and management endpoints

**Deliverables:**
- [x] School database table with sample data (130 schools from template)
- [x] Admin interface for managing schools with FluentUI components
- [x] Fuzzy matching algorithm for school names
- [x] FluentDataGrid with pagination, sorting, and professional styling
- [x] CSV import system with logo download capabilities
- [x] Complete CRUD operations for school management

### Task 1.3: System Management Authentication
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 1.1

- [x] Implement management authentication with master password
- [x] Create `/api/management/*` API routes (due to Azure Static Web Apps `/admin` restrictions)
- [x] Add token-based authentication for management operations
- [x] Create management interface and basic dashboard
- [x] Implement logout and token cleanup
- [x] Add comprehensive authentication middleware to school management endpoints
- [x] Implement secure login page with FluentUI components
- [x] Add automatic token validation and redirect logic
- [x] Resolve cross-browser compatibility issues (Firefox/Opera Linux form binding)
- [x] Note: Separate from auction join code system

**Deliverables:**
- [x] Secure management API with password protection
- [x] Token-based authentication system with 8-hour expiry
- [x] Professional FluentUI login interface
- [x] Automatic authentication state management and redirects
- [x] Protected management dashboard structure

### Task 1.4: Database Architecture & Entity Framework Debugging
**Priority:** Critical  
**Estimated Effort:** 8-12 hours  
**Dependencies:** Task 1.1, 1.2, 1.3

- [x] Resolve persistent SaveChanges errors in Entity Framework
- [x] Fix foreign key constraint issues with CreatedByUserId nullable relationships  
- [x] Implement proper database migration handling for production deployments
- [x] Debug and resolve authentication header inconsistencies between API functions
- [x] Add comprehensive error logging and debugging infrastructure
- [x] Ensure proper Entity Framework retry policies for Azure SQL Database
- [x] Create manual migration endpoints for production database fixes
- [x] Resolve cross-browser authentication token handling issues

**Deliverables:**
- [x] Stable database operations without SaveChanges failures
- [x] Consistent authentication across all API endpoints 
- [x] Proper Entity Framework configuration for production Azure SQL Database
- [x] Comprehensive debugging and error logging infrastructure

---

## Phase 2: Auction Creation & Configuration (Weeks 2-3)

### Task 2.1: Core Auction Entities
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 1.2

- [x] Create Auction, User, Team database schema
- [x] Implement basic Auction entity with join code generation
- [x] Add Auction status management (Draft, InProgress, Complete)
- [x] Create auction repository with basic CRUD operations
- [x] Implement join code validation and uniqueness

**Deliverables:**
- [x] Auction database schema implemented
- [x] Join code generation system
- [x] Basic auction creation functionality

### Task 2.2: CSV Import System ✅
**Priority:** Critical  
**Estimated Effort:** 10-12 hours  
**Dependencies:** Task 2.1

- [x] Create CSV parsing service for draft templates with comprehensive XML documentation
- [x] Implement AuctionSchool entity and mapping using primary constructor syntax
- [x] Build school matching interface using FluentDialog for confirmation prompts
- [x] Add validation for required CSV columns with FluentValidation or DataAnnotations
- [x] Create import error handling with structured logging and user feedback
- [x] Implement data preview using FluentDataGrid before final import
- [x] Add enhanced UI with summary statistics and filtering for large datasets
- [x] Fix multipart form parsing and CSV extraction logic
- [x] Replace FluentUI InputFile with working standard HTML InputFile
- [ ] Add unit tests for CSV parsing and validation logic

**Deliverables:**
- [x] CSV upload and parsing functionality with robust error handling
- [x] School matching with Auction Master confirmation and fuzzy matching
- [x] AuctionSchool data populated from imports with proper validation
- [x] Enhanced UI for reviewing large result sets (100+ schools)

### Task 2.3: Roster Design Interface ✅
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 2.2

- [x] Create RosterPosition entity using primary constructor syntax with XML documentation
- [x] Build roster design UI with auto-creation and inline editing functionality
- [x] Implement position validation against available schools with structured logging
- [x] Add color picker with predefined color palette for position coding
- [x] Create roster preview with team slot calculation and validation
- [x] Implement validation warnings with FluentMessageBar for impossible rosters
- [x] Enhanced UX with auto-creation from CSV import, click-to-edit, and responsive layout
- [ ] Add unit tests for roster validation business logic

**Deliverables:**
- [x] Advanced roster design interface for Auction Masters with auto-creation
- [x] Position validation system with comprehensive feedback
- [x] Predefined color palette system with 10 distinct colors
- [x] Inline editing with PUT API endpoint for position updates

### Task 2.4: Auction Setup Completion ✅
**Priority:** High  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 2.3

- [x] Create auction configuration with budget setting per team
- [x] Implement nomination order configuration (Random, Alphabetical, Manual)
- [x] Add additional auction settings (proxy bidding, budget warnings)
- [x] Create enhanced 4-step setup wizard with auto-progression
- [x] Build comprehensive auction validation and review system
- [x] Add budget analysis with recommendations based on roster size
- [ ] Add recalculation logic for replacement values (future enhancement)

**Deliverables:**
- [x] Complete 4-step auction setup workflow with professional UI
- [x] Budget configuration with intelligent recommendations  
- [x] Multiple nomination order strategies
- [x] Enhanced validation system preventing invalid auctions
- [x] Responsive design with FluentUI components

---

## Phase 3: User Join & Role Management (Week 4)

### Task 3.1: Join Auction Flow
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 2.1

- [ ] Create join auction page with code entry
- [ ] Implement user registration with display name
- [ ] Add case-insensitive name matching for returning users
- [ ] Create user session management
- [ ] Implement basic auction participant view
- [ ] Add join validation (auction exists, not full, etc.)

**Deliverables:**
- Join auction functionality
- User registration and session management
- Basic participant dashboard

### Task 3.2: Role Assignment System
**Priority:** Critical  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 3.1

- [ ] Create UserRole entity and management system
- [ ] Build Auction Master interface for role assignment
- [ ] Implement team assignment for coaches
- [ ] Add proxy coach assignment to multiple teams
- [ ] Create role-based permission enforcement
- [ ] Build role switching UI for proxy coaches

**Deliverables:**
- Role assignment interface for Auction Masters
- Permission system enforcing role capabilities
- Proxy coach team switching functionality

### Task 3.3: Reconnection System
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 3.2

- [ ] Implement reconnection approval workflow
- [ ] Create pending reconnection queue for Auction Master
- [ ] Add state synchronization on reconnection
- [ ] Build loading screen for state sync
- [ ] Implement connection status tracking
- [ ] Add reconnection notifications

**Deliverables:**
- Reconnection approval system
- State synchronization on user return
- Connection status management

---

## Phase 4: Basic SignalR Integration (Week 5)

### Task 4.1: SignalR Hub Foundation
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 3.3

- [ ] Create AuctionHub with basic connection management
- [ ] Implement auction-specific SignalR groups
- [ ] Add user authentication to SignalR connections
- [ ] Create connection/disconnection event handling
- [ ] Build basic real-time status updates
- [ ] Add error handling for SignalR operations

**Deliverables:**
- Working SignalR hub with auction groups
- Connection management system
- Basic real-time communication

### Task 4.2: Admin Panel Real-time Features
**Priority:** High  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 4.1

- [ ] Create Auction Master management panel (separate tab)
- [ ] Implement real-time connection status display using FluentDataGrid
- [ ] Add proxy coach assignment visualization with FluentCard components
- [ ] Create reconnection approval interface using FluentDialog
- [ ] Build management-only SignalR broadcasts with structured logging
- [ ] Add auction control buttons using FluentButton (pause, end, etc.)
- [ ] Implement comprehensive unit tests for SignalR hub methods

**Deliverables:**
- Auction Master management panel with real-time updates
- Connection status monitoring
- Administrative controls for auction management

---

## Phase 5: Real-time Bidding System (Weeks 6-7)

### Task 5.1: Nomination System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 4.2

- [ ] Create NominationOrder entity and management
- [ ] Implement turn-based nomination logic
- [ ] Build school nomination interface
- [ ] Add nomination validation (roster space, budget)
- [ ] Create automatic turn advancement
- [ ] Implement nomination broadcasts via SignalR

**Deliverables:**
- Turn-based nomination system
- Real-time nomination updates
- Nomination validation and turn management

### Task 5.2: Bidding System
**Priority:** Critical  
**Estimated Effort:** 10-12 hours  
**Dependencies:** Task 5.1

- [ ] Create BidHistory entity and tracking
- [ ] Implement bid placement with validation
- [ ] Build bidding interface with current high bid display using FluentTextField and FluentButton
- [ ] Add budget constraint enforcement with FluentValidation and user feedback
- [ ] Create pass functionality using FluentButton for all users
- [ ] Implement Auction Master end-bidding controls with FluentDialog confirmations
- [ ] Add comprehensive unit tests for bidding validation logic

**Deliverables:**
- Complete bidding system with validation
- Real-time bid updates
- Budget enforcement and pass functionality

### Task 5.3: Draft Completion System
**Priority:** Critical  
**Estimated Effort:** 8-10 hours  
**Dependencies:** Task 5.2

- [ ] Create DraftPick entity for final results
- [ ] Implement school assignment to roster positions
- [ ] Build auto-assignment with manual override
- [ ] Add roster validation and completion tracking
- [ ] Create auction end detection logic
- [ ] Implement final results compilation

**Deliverables:**
- School assignment to roster positions
- Auto-assignment with manual override capability
- Auction completion detection and results

### Task 5.4: Results Export System
**Priority:** High  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 5.3

- [ ] Create CSV export functionality matching sample format
- [ ] Implement results download for all participants
- [ ] Add real-time results view during auction
- [ ] Create historical results access
- [ ] Build results formatting and validation

**Deliverables:**
- CSV export matching required format
- Results download functionality
- Real-time results display

---

## Phase 6: Admin Management & Cleanup (Week 8)

### Task 6.1: Auction Management Interface
**Priority:** Medium  
**Estimated Effort:** 6-8 hours  
**Dependencies:** Task 4.2

- [x] Build comprehensive auction list for management interface
- [x] Implement auction deletion with confirmations  
- [x] Add authentication token handling for management operations
- [x] Fix auction creation with proper database persistence
- [x] Resolve SaveChanges issues with nullable foreign keys
- [ ] Add auction status filtering and search
- [ ] Create bulk cleanup operations
- [ ] Add auction archiving functionality
- [ ] Build auction details view for management interface

**Deliverables:**
- [x] Complete management auction interface with FluentDataGrid
- [x] Working auction creation and deletion operations
- [x] Proper authentication token management across all operations
- [ ] Bulk operations for cleanup
- [ ] Safe deletion with confirmations

### Task 6.2: School Logo Management
**Priority:** Low  
**Estimated Effort:** 4-6 hours  
**Dependencies:** Task 1.2

- [ ] Implement logo loading strategy: External URL → Internal file → Placeholder fallback
- [ ] Add logo URL testing and validation with preview
- [ ] Create individual logo upload interface for broken external URLs
- [ ] Implement bulk ZIP logo upload as last resort option
- [ ] Add school statistics and logo availability tracking

**Deliverables:**
- Enhanced school management with logo features
- Bulk import capabilities
- School usage analytics

### Task 6.3: Audit and Monitoring
**Priority:** Medium  
**Estimated Effort:** 4-6 hours  
**Dependencies:** All previous tasks

- [ ] Create AdminAction logging system
- [ ] Implement comprehensive audit trails
- [ ] Add performance monitoring for SignalR
- [ ] Create error logging and alerting
- [ ] Build system health dashboard
- [ ] Add usage analytics and reporting

**Deliverables:**
- Complete audit logging system
- Performance monitoring
- System health and usage analytics

---

## Testing Strategy Per Phase

### Development Testing Approach
- **Local Development:** Basic UI and business logic testing
- **Deployment Testing:** All real-time and multi-user features
- **Multi-User Scenarios:** Test with multiple browser windows/devices
- **Edge Case Testing:** Network disconnections, invalid data, concurrent operations

### Phase-Specific Testing
1. **Phase 1-2:** School management, CSV imports, auction setup
2. **Phase 3:** User joining, role assignments, permissions
3. **Phase 4:** SignalR connections, real-time updates
4. **Phase 5:** Complete auction flow with multiple users
5. **Phase 6:** Admin operations, cleanup, edge cases

---

## Risk Mitigation

### High-Risk Areas
- **SignalR Integration:** Complex real-time state management
- **Multi-User Bidding:** Concurrent access and race conditions
- **State Synchronization:** Maintaining consistency across clients
- **Budget Validation:** Complex business logic enforcement

### Mitigation Strategies
- **Early SignalR Testing:** Implement basic real-time features early
- **Server-Side Validation:** All critical business logic on server
- **Incremental Complexity:** Start with simple features, add complexity
- **Frequent Deployment:** Test each feature in production environment

---

## Success Criteria Per Phase

### Phase 1 Success
- [x] Deployed app accessible via Azure
- [x] Schools can be added and managed with full CRUD operations
- [x] FluentUI framework properly integrated with pagination and sorting
- [x] CSV import system functional with logo downloads
- [x] Admin authentication working with secure token-based system
- [x] Entity Framework properly configured with Azure SQL Database
- [x] Cross-browser compatibility resolved for all supported browsers
- [x] Database operations stable without SaveChanges errors
- [x] Comprehensive auction management interface operational

### Phase 2 Success
- [ ] CSV upload and school matching functional
- [ ] Roster design with position validation
- [ ] Complete auction setup workflow

### Phase 3 Success
- [ ] Users can join auctions via join code
- [ ] Role assignment by Auction Master
- [ ] Reconnection system working

### Phase 4 Success
- [ ] Real-time connection status
- [ ] Admin panel with live updates
- [ ] SignalR communication established

### Phase 5 Success
- [ ] Complete auction with multiple users
- [ ] Bidding system with all validations
- [ ] Results export in correct format

### Phase 6 Success
- [ ] Admin cleanup operations
- [ ] Audit logging functional
- [ ] Production-ready system

This task breakdown provides achievable milestones while building toward the complete auction system. Each task includes specific deliverables and can be tested independently on the deployed Azure environment.