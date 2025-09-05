# Leagify Fantasy Auction

A real-time auction draft web application for fantasy sports, specifically designed for the NFL Draft League. The system allows multiple users to participate in live auctions bidding on college schools, with real-time updates and comprehensive roster management.

## Project Status - Phase 1 Complete ✅

### Recently Completed (December 2024)
- **FluentUI Framework Integration**: Restored Microsoft Fluent UI Blazor components with proper MIME configuration
- **School Management System**: Complete CRUD operations with professional data grid interface
- **Data Grid with Pagination**: FluentDataGrid with 20 items per page, sortable columns, and modern styling
- **CSV Import System**: Upload CSV files with automatic logo downloads and school matching
- **Logo Management**: External URL loading with fallback handling and preview functionality
- **School Database**: Pre-loaded with 130 schools from template with logo URLs

### Current Architecture
- **Frontend**: Blazor WebAssembly (WASM) with Microsoft Fluent UI components
- **Backend**: Azure Functions within Static Web Apps
- **Database**: Azure SQL Database (in-memory storage for development)
- **Hosting**: Azure Static Web Apps with CI/CD
- **Real-time**: Azure SignalR Service (planned for Phase 4-5)

### Live Demo
🔗 **[https://jolly-meadow-0b4450210.2.azurestaticapps.net](https://jolly-meadow-0b4450210.2.azurestaticapps.net)**

Access the school management interface at `/management/schools` to see the completed Phase 1 functionality.

## Next Steps - Phase 2 Planning
- System management authentication
- Core auction entities and configuration  
- Advanced CSV import with school matching
- Roster design interface

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

✅ **School Management**
- Professional FluentDataGrid with pagination (20 items per page)
- Full CRUD operations (Create, Read, Update, Delete)
- CSV import with automatic logo downloads
- Logo URL management with preview functionality
- Sortable columns for better data exploration
- 130 pre-loaded schools with logo assets

✅ **Modern UI Framework**  
- Microsoft Fluent UI Blazor components throughout
- Consistent design language and professional styling
- Responsive data grids and forms
- Modern dialog systems and input controls

✅ **Azure Infrastructure**
- Deployed on Azure Static Web Apps
- CI/CD pipeline with GitHub Actions
- Azure Functions for API endpoints
- Production-ready hosting environment

## Technology Stack

- **C# 13** with .NET 8+ optimizations
- **Blazor WebAssembly** for client-side functionality  
- **Microsoft Fluent UI** for professional business interface
- **Azure Static Web Apps** for deployment and hosting
- **Azure Functions** for serverless API endpoints
- **Azure SQL Database** for persistent data storage (planned)

This project follows enterprise development practices with comprehensive documentation, structured task management, and production deployment from day 1.
