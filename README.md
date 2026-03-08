# Leagify Fantasy Auction

A real-time auction draft web application for fantasy sports, specifically designed for the NFL Draft League. The system allows multiple users to participate in live auctions bidding on college schools, with real-time updates and comprehensive roster management.

## Project Status - Phases 1-6 Complete ✅ / Phase 7 In Progress

### Recently Completed
- **Phase 1-2 (Foundation & Setup)**: Complete CRUD operations for schools, Fluent UI integration, CSV import with logo downloads, and complete auction setup workflow wizard.
- **Phase 3-4 (Users & Real-time Base)**: Join auction flows, role-based permissions, robust session management, and foundational SignalR integration.
- **Phase 5 (Real-time Bidding)**: Turn-based nomination system, real-time bidding via SignalR with budget enforcement, and draft completion with auto-assignment.
- **Phase 6 (Admin & Monitoring)**: Comprehensive auction management, audit logging, system health dashboard with real-time metrics, and bulk logo uploads.

### Current Architecture
- **Frontend**: Blazor WebAssembly (WASM) with Microsoft Fluent UI components
- **Backend**: Azure Functions within Static Web Apps
- **Database**: Azure SQL Database
- **Hosting**: Azure Static Web Apps with CI/CD
- **Real-time**: Azure SignalR Service (Active)

### Live Demo
🔗 **[https://jolly-meadow-0b4450210.2.azurestaticapps.net](https://jolly-meadow-0b4450210.2.azurestaticapps.net)**

Access the admin dashboard at `/management/login` and try the participant flow at `/join`.

## Next Steps - Phase 7: Production Readiness
- **SignalR Connection Management**: Idle timeouts and zombie connection cleanup to optimize Azure database costs.
- **Auction Control Features**: Admin controls to pause, resume, and end auctions prematurely.
- **Network Resilience**: Comprehensive testing of disconnection/reconnection scenarios.
- **Production Pre-flight**: Security audits, UX polishing based on full test auctions, and preparation for first real draft.

## Development Commands

```bash
# Build the project
dotnet build

# Run locally (limited functionality - requires Azure deployment for full testing)
dotnet run --project LeagifyFantasyAuction

# Deploy to Azure (automatic via GitHub Actions)
git push origin main
```

## Documentation

- **[DEVELOPMENT-TASKS.md](DEVELOPMENT-TASKS.md)**: 6-phase implementation plan with detailed task breakdown
- **[PRODUCT-DESIGN.md](PRODUCT-DESIGN.md)**: Complete technical architecture and user flows  
- **[DATABASE-ERD.md](DATABASE-ERD.md)**: Database schema with 12 entities and relationships
- **[CLAUDE.md](CLAUDE.md)**: Development guidelines and architectural decisions

## Key Features Implemented

✅ **Core Auction & Real-time Bidding**
- Turn-based nomination system with automatic advancement
- Real-time bidding via Azure SignalR with budget enforcement
- Draft completion system with intelligent auto-assignment
- Live results export to CSV

✅ **School & Roster Management**
- Professional FluentDataGrid with pagination and sortable columns
- CSV import with automatic logo downloads and fuzzy matching
- Advanced roster design interface with auto-creation
- Pre-loaded with 130 schools with logo assets

✅ **Admin & System Monitoring**
- Comprehensive auction setup wizard and management interface
- Audit logging and system health dashboard with real-time metrics
- Granular role assignment (Auction Master, Team Coach, Proxy Coach, Viewer)

✅ **Modern UI Framework & Architecture**  
- Microsoft Fluent UI Blazor components throughout
- Deployed on Azure Static Web Apps with Azure Functions API

## Technology Stack

- **C# 13** with .NET 8+ optimizations
- **Blazor WebAssembly** for client-side functionality  
- **Microsoft Fluent UI** for professional business interface
- **Azure Static Web Apps** for deployment and hosting
- **Azure Functions** for serverless API endpoints
- **Azure SQL Database** for persistent data storage
- **Azure SignalR Service** for real-time capabilities

This project follows enterprise development practices with comprehensive documentation, structured task management, and production deployment from day 1.
