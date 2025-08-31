# Leagify Auction Drafter
An auction draft webapp for Leagify, specifically the NFL Draft League
The goal is for this repository will be a Blazor WASM (Webassembly) C# SignalR web application.





## General design:
+ When a user creates a new auction, they become the "Auction Master" for that event.
+ The application will generate a unique "Join Code" for each auction.
+ The Auction Master shares this code with other participants.
+ Other users can join the auction by entering the Join Code. Once they have joined, the Auction Master can assign them one or more of the following roles:
  - **Team Coach:** Can bid on schools and manage a team roster.
  - **Proxy Coach:** Can bid on behalf of a Team Coach.
  - **Auction Viewer:** Has read-only access to the auction.
+ A user can have multiple roles (e.g., be a Team Coach for one team and a Proxy Coach for another in the same auction).
+ The auction can be viewed by anyone who has the join code.

+ **User Roles and Permissions:** Roles are assigned by the Auction Master after a user joins via the join code.

| Action                       | Auction Master | Team Coach | Proxy Coach | Auction Viewer |
| ---------------------------- | :------------: | :--------: | :---------: | :------------: |
| Create / Configure Auction   |       ✅       |     ❌     |      ❌     |       ❌       |
| Upload Draft Template (CSV)  |       ✅       |     ❌     |      ❌     |       ❌       |
| Assign Roles to Users        |       ✅       |     ❌     |      ❌     |       ❌       |
| Start / Pause / End Auction  |       ✅       |     ❌     |      ❌     |       ❌       |
| Nominate a School            |       ❌       |     ✅     |      ✅     |       ❌       |
| Bid on a School              |       ❌       |     ✅     |      ✅     |       ❌       |
| View All Rosters & Budgets   |       ✅       |     ✅     |      ✅     |       ✅       |
| Download Final Results       |       ✅       |     ✅     |      ✅     |       ✅       |

+ Team coaches pick from a "draft board".
+ At this time, the draft board loads information from a CSV draft template file uploaded by the auction master.  The format of the draft template is similar to the template at the root of this repo, named "SampleDraftTemplate.csv"
+ The source of truth would not be the CSV. The CSV is the starting point, and the information would populate a database corresponding to this auction.
+ The auction master should be able to design the roster for the auction.
  - **Implementation Note:** The roster builder UI should allow the Auction Master to add or remove position slots. The available position types (e.g., "Big Ten", "SEC", "Flex") should be dynamically populated from the unique values found in the `LeagifyPosition` column of the uploaded draft template. This prevents typos and ensures all roster slots are valid. There should also be the option for a "Flex" position which would allow any school.
+ The Draft Template contains several fields. I will explain them here.
  - School : The school that the draft prospects come from. The thing that will eventually score points.
  - Conference : The conference that the school plays in. For example, Wisconsin (School) plays in the Big Ten (Conference), and LSU (School) plays in the SEC (Conference).
  - ProjectedPoints : This is a projection of how many points this school may score this year. The accuracy of this number varies from year to year.
  - NumberOfProspects : The number of potential draft prospects that have appeared on draft prospect boards 
  - SchoolURL : A URL representing an SVG image that represents the school. The goal 
  - SuggestedAuctionValue : This is supposed to represent a suggested auction value. It is not filled at this point, but might be at some point in the future.
  - LeagifyPosition : Some conferences are large enough to be their own "position" in the draft.Not every conference is large enough to merit its own position, however, so some conferences are but into larger bins, like "RandomSmallSchool". The Notre Dame school doesn't have a conference, so we've elected them to go directly into the "Flex" position instead of a traditional position.
  - ProjectedPointsAboveAverage : This is a calculated number representing the average value of points that a member of this school's conference is projected to have in the draft.
  - ProjectedPointsAboveReplacement : This is a calculated value between this school's projected value compared to the "Replacement Value" of a school from its position.
  - AveragePointsForPosition : This is the average number of points a school in this position
  - ReplacementValueAverageForPosition : The "Replacement Value" of a position is the value of the schools that are left after all of the top schools are presumably selected by the players. For example, if 6 schools from the "ACC" position are projected to be selected in the auction, this is the value of the 7th ACC school, meaning the school you could potentially pick after everyone else has chosen a school.
+ It would be nice for the Auction Master to be able to re-calculate the following fields based on the number of team coaches in the draft:
  - ProjectedPointsAboveReplacement
  - ReplacementValueAverageForPosition
+ Items up for auction will have several main properties (similar to Player and Position)
+ Each bidder will be trying to fill a "roster" of "positions"
+ Each bidder will have a budget, which is editable before the auction begins, but not afterwards.
+ Bidders agree to join an "auction" and the auction order is set up in a normal order.
+ The auction includes a known list of potential "players" (no values are pre-assigned, at this point)
+ Each bidder can view:
  - The remaining "players" available
  - The "player" up for bid
  - The current bid
  - Current highest bidder
  - The bidder's "roster"
  - The bidder's available money


+ **Data Calculation Logic:**
  - After the Auction Master uploads the template, the application should respect any data that is uploaded initially, but it should provide the auction master the ability to recalculate it and calculate the following metrics. These should be stored with the auction data for display.
  - **`ReplacementValueAverageForPosition`**: To calculate this, the application must first determine the number of "starters" for a position. This is a configurable number per position set by the Auction Master (e.g., ACC = 1 position). The replacement value is the `ProjectedPoints` of the best remaining school after each coach selected one of the top schools.
    - Example: In a league where there are 6 team coaches and each of them has one ACC position in thFor the "ACC" position with 6 starters, find the ACC school with the 7th highest `ProjectedPoints`. That `ProjectedPoints` value is the `ReplacementValueAverageForPosition` for every school in the ACC.
  - **`ProjectedPointsAboveReplacement`**: After the replacement value is calculated above, you can calculate the projected value of the school to the replacement value. For each school:
    - `ProjectedPointsAboveReplacement` =  `ProjectedPoints`(for that school) **minus** `ReplacementValueAverageForPosition` (for the Position that the school belongs to)



## The Auction process
+ At the beginning of the auction, each team coach has a budget, which is usually the same, like 200 dollars, unless the auction master determines that one team should have a seperate budget because they arrived late or need to have a handicap. Currently, changing the positions is not possible, but changing the number of schools in that position should be configurable.
+ The auction master should be able to color code what the positions look like on the screen. It's OK to be able to pick from a list of complimentary colors. There won't be more than 8 different positions, so it would be fine to pick from a palette that changes all of the positional colors instead of picking indivudal positions.
+ The auction master determines the order for 
+ The auction master should be able to design the roster that the auction. The default roster positions are:
  - Big Ten : A school from the Big Ten Conference
  - Big Ten : A school from the Big Ten Conference
  - SEC : A school from the SEC Conference
  - SEC : A school from the SEC Conference
  - Big 12 : A school from the Big 12 Conference
  - ACC : A school from the ACC Conference
  - Small School : A school from any conference other than the Big Ten Conference, the SEC Conference, the ACC Conference, the Big 12, 
  - Flex : Any school
  - Flex : Any school
  - Flex : Any school
 
+ A group of team coaches will be bidding on schools.
+ Each bidder can put a "School" up for bid.
+ Bidding continues until all other team coaches pass or no other team coach can afford to bid (determined by remaining spots in roster multiplied by minimum bid amount).
+ After bidding completes, the "School" is placed on the bidders roster, and the amount is deducted from the bidder's budget.
+ If a bidder's roster is full, they can no longer nominate players for bid, and they can no longer win auctions for schools.
+ Bidding continues until each team coach's roster is full.
+ The minimum bid for a school is 1 dollar. There is no maximum bid. Technically, the maximum bid for a school would be the full amount of the team coach's budget minus one dollar for every open position on their roster.
+ The only reason a roster spot should remain open is if there are no more schools available for that position.
+ Bidding mechanism: There is no time limit until the auction master presses a button that indicates "going once", "going twice", and "sold", with about 2 seconds between each designation.  If a coach bids during this period, the "going, going, gone" process cancels until the auction master starts it again. A coach can manually withdraw themselves from the bidding, or they can be mathematically eliminated from bidding by not having enough money to bid on the school.
+ **Post-Auction Win:** After winning, the coach must immediately assign the school to a valid, open roster position. The system will enforce roster validation rules (e.g., an SEC school can only go in an "SEC" or "Flex" position).
+ **End of Auction:** The auction concludes when all teams have filled all their roster spots, or when no team can legally nominate a player they can afford.
+ At the completion of the auction, a CSV can be downloaded, which contains:
  - The bidder name
  - The school name
  - The conference position that the bidder put the school into
  - The auction cost
  - A variety of other school information that comes directly from the uploaded CSV
  - An example of the desired output can be found in this repository with the name "SampleFantasyDraft.csv".



# The Leagify NFL Draft Game (This occurs outside of this application, during the NFL draft. A separate app administers the game, using the CSV output that this application creates.)
+ The goal is to have the most points at the end of the draft.
+ “Team coaches” have a roster of schools
+ Each school has a Position. Generally, college football conferences correspond to positions, but there are also combined conference positions and "Flex" positions that accomodate any school.
+ Schools have athletes. Those athletes are drafted in the NFL Draft. Those draft picks correspond to a value chart created by Leagify.
+ Bonus points are provided for athletes that are selected in the NFL Draft the result of a trade, which adds a little variance to the game. The highest scoring coach at the end of the NFL Draft is the winner. This game runs once per year, corresponding to the NFL Draft.



Technologies:
+ C# / .NET
+ Blazor WASM for the client-side UI and logic.
+ SignalR for real-time communication. Jules, this will be key for the following events:
  - Notifying all clients when a new player is nominated.
  - Broadcasting new bids and the current high bidder in real-time.
  - Announcing the winner of a school.
  - Updating all clients' views of rosters and budgets instantly.
+ There should be a document that explains how to run a debug instance of this app in GitHub inside of GitHub Codespaces
+ There should be another document that describes how to deploy this code on a combination of three services, Azure Static Web Apps, Azure SignalR service, and Azure SQL Database
+ How the services are supposed to work together:
  - A user accesses the app hosted on Static Web Apps.
  - Their browser connects to the Azure SignalR Service for real-time updates.
  - When team coaches place a bid, the request goes to the C# API (hosted on Azure Functions within the Static Web App), which validates the bid, updates the Azure SQL Database, and then tells the Azure SignalR Service to broadcast the new bid to all connected users.

### A Note on Application State
The server should always be the single source of truth for all auction data (current bidder, bid amount, budgets, rosters, etc.). The Blazor WASM client is responsible for displaying this state and sending user actions (like placing a bid) to the server. If a user temporarily disconnects and reconnects, the client should always re-sync its state from the server.

 
# Development Plan: User Stories

To help guide development, we can break the project down into the following stories.

### Epic: Auction Setup & Configuration

* **As an Auction Master, I want to** create a new auction so that I can prepare for a draft.
* **As an Auction Master, I want to** upload a CSV of school data so that the auction has players to draft.
* **As an Auction Master, I want to** define the roster structure for all teams (e.g., 2x SEC, 1x ACC, 3x Flex) so that the draft rules are set.
* **As an Auction Master, I want to** manage user roles, assigning coaches and proxy coaches to my auction.
* **As an Auction Master, I want to** adjust the budget for any individual team coach.
* **As an Auction Master, I want to** start, pause, and end the auction.
* **As an Auction Master, I want to** be able to have the role of Auction Master, Team Coach, and Proxy Coach.

### Epic: The Auction Draft

* **As a Team Coach, I want to** join a specific auction so I can participate.
* **As a Team Coach, I want to** see the draft board of all available schools and their stats.
* **As a Team Coach, I want to** nominate a school for bidding when it is my turn.
* **As a Team Coach, I want to** place bids on the currently nominated school.
* **As an Auction Master, I want to** control the end of bidding for a school using a "Going, Going, Gone" mechanic.
* **As a Team Coach, I want to** see my current roster and remaining budget update in real-time.
* **As a user with multiple roles (Coach/Proxy), I want to** easily switch which team I am bidding for.

### Epic: Post-Auction

* **As any participant, I want to** view the final draft results after the auction is complete.
* **As any participant, I want to** download the final draft results as a CSV file.


## Appendix: Conceptual Data Model

To support the features above, the application will need to manage several key entities in its database. This is not a final schema, but a guide for development.

* **User:** Represents a participant in the auction. Since there's no login system, this could be a simple identifier generated when a user joins with a code, linked to the roles assigned by the Auction Master.
* **Auction:** The main container for a single draft event. It would have a status (e.g., Not Started, In Progress, Complete).
* **Team:** Represents a coach's entry in a specific `Auction`. It would hold the `User` ID, the team's budget, and be linked to the `Auction`.
* **School (Player):** The information loaded from the CSV, linked to a specific `Auction`. Contains all the stats like `ProjectedPoints`, `Conference`, etc.
* **RosterDesign:** Defines the structure of a team's roster for an `Auction` (e.g., one row for each "SEC", "Flex" slot).
* **DraftPick:** A record that links a `Team`, a `School`, and the final `AuctionCost`. This forms the final draft board.
