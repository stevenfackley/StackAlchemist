# StackAlchemist: ASCII Wireframes

This document provides a visual representation of the StackAlchemist user interface using ASCII art.

---

## 1. Landing Page (Hero)
```text
+----------------------------------------------------------------------------------+
| [Logo] StackAlchemist        [About] [Pricing] [Story] [Docs] [Sign In]          |
+----------------------------------------------------------------------------------+
|                                                                                  |
|                              SYNTHESIZE YOUR PLATFORM.                           |
|                Transform natural language into deployable architecture           |
|                           with a compile guarantee.                              |
|                                                                                  |
|                     [ Built for fast handoffs summary panel ]                    |
|                                                                                  |
|               [.NET 10] [Next.js 15] [PostgreSQL] [Supabase] [Dapper]           |
+----------------------------------------------------------------------------------+
|                                                                                  |
|  LAUNCH CONSOLE                                                                  |
|  [ Simple Mode ] [ Advanced Mode ]                                               |
|                                                                                  |
|  [ Prompt textarea ]                                                             |
|  [ Prompt Builder ]                                                              |
|  [ Synthesize ]                                                                  |
|                                                                                  |
|  [ What You Walk Away With ]      [ Flow / Next Steps ]                          |
+----------------------------------------------------------------------------------+
```

---

## 2. Simple Mode: AI Schema Generation
```text
+-------------------------------------------------------------+
|  [Logo] < Back to Home                 [User Profile]       |
+-------------------------------------------------------------+
|  Input: "Create a subscription-based gym management app."   |
+-------------------------------------------------------------+
|                                                             |
|   [ GENERATING SCHEMA... 75% ]                              |
|   > Extracting Entities: User, Plan, Subscription, Check-in |
|   > Mapping Relationships: User (1:M) Subscription          |
|                                                             |
|   +-----------------------+      +-----------------------+  |
|   |         User          |      |         Plan          |  |
|   +-----------------------+      +-----------------------+  |
|   | - id (UUID)           | <--- | - id (UUID)           |  |
|   | - email (String)      |      | - name (String)       |  |
|   +-----------------------+      | - price (Decimal)     |  |
|                                  +-----------------------+  |
|                                                             |
|   [ EDIT SCHEMA ] [ CONFIRM & PROCEED TO STACK SELECTION ]  |
+-------------------------------------------------------------+
```

---

## 3. Advanced Mode: Manual Entity Wizard (Stepper)
```text
+-------------------------------------------------------------+
|  [Logo] Step 1: Entities -> Step 2: API -> Step 3: Billing  |
+-------------------------------------------------------------+
|                                                             |
|  [+] Add New Entity                                         |
|                                                             |
|  Entity Name: [ Product         ]                           |
|  Fields:                                                    |
|  [ id      ] [ UUID      ] [PK] [X]                         |
|  [ name    ] [ String    ] [  ] [X]                         |
|  [ price   ] [ Decimal   ] [  ] [X]                         |
|  [+] Add Field                                              |
|                                                             |
|  Relationships:                                             |
|  [ Product ] [ Belongs To ] [ Category ] [X]                |
|  [+] Add Relationship                                       |
|                                                             |
|  [ PREVIOUS ]                                     [ NEXT ]  |
+-------------------------------------------------------------+
```

---

## 4. Stack Selection & Tier Checkout
```text
+-------------------------------------------------------------+
|  [Logo] Select Your Tier                [User Profile]      |
+-------------------------------------------------------------+
|                                                             |
|  [ TIER 1: BLUEPRINT ]   [ TIER 2: BOILERPLATE ]  [ TIER 3 ]|
|        $299                    $599                 $999    |
|  --------------------    ---------------------    ----------|
|  * Schema JSON           * T1 Features            * T2 Feat.|
|  * API Specs             * FULL SOURCE CODE       * AWS CDK |
|  * SQL Scripts           * COMPILE GUARANTEE      * RUNBOOK |
|                                                             |
|  [ SELECT ]              [ RECOMMENDED ]          [ SELECT ]|
|                                                             |
+-------------------------------------------------------------+
|  [ PROCEED TO STRIPE CHECKOUT ]                             |
+-------------------------------------------------------------+
```

## 4A. Standalone Pricing Page Header
```text
+-------------------------------------------------------------+
| [Logo] StackAlchemist   [Home] [About] [Story] [Build]     |
+-------------------------------------------------------------+
```

---

## 5. Live Generation Status (The "Alchemist's Cauldron")
```text
+-------------------------------------------------------------+
|  [Logo] Transmuting Your Stack...       [User Profile]      |
+-------------------------------------------------------------+
|                                                             |
|   STATUS: [=========================>        ] 65%          |
|                                                             |
|   LOGS:                                                     |
|   [14:02:11] Retrieving Master .NET/Next.js Templates...    |
|   [14:02:15] Injecting Handlebars Variables... DONE         |
|   [14:02:22] Synthesizing C# Dapper Queries... DONE         |
|   [14:02:35] RUNNING COMPILE GUARANTEE CHECK...             |
|   [14:02:40] > dotnet build: SUCCESS                        |
|   [14:02:42] > npm run build: SUCCESS                       |
|   [14:02:45] Packing Archive...                             |
|                                                             |
|   [ ( ) ANALYZING ] [ (X) TRANSMUTING ] [ ( ) VALIDATING ]  |
+-------------------------------------------------------------+
```

---

## 6. Final Delivery & Download
```text
+-------------------------------------------------------------+
|  [Logo] Transmutation Complete!         [User Profile]      |
+-------------------------------------------------------------+
|                                                             |
|   YOUR STACK IS READY FOR DEPLOYMENT                        |
|                                                             |
|   [ DOWNLOAD SOURCE CODE (.ZIP) ]                           |
|   (Expires in 24 Hours)                                     |
|                                                             |
|   [ VIEW API DOCUMENTATION ] [ VIEW DEPLOYMENT RUNBOOK ]    |
|                                                             |
|   -------------------------------------------------------   |
|   NEXT STEPS:                                               |
|   1. Unzip the archive.                                     |
|   2. Run 'supabase link' in the root directory.             |
|   3. Run 'npm install' and 'dotnet watch run'.              |
|                                                             |
+-------------------------------------------------------------+
```
