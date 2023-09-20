# Mailbox-Service Mass Grant Tool

Occasionally we need to send out rewards packages to players en masse.  Portal has some tooling but is currently intended for individual or small batch processing.  Long-term it would be great to provide mass granting functionality to Portal, but in the meantime, this tool will allow you to craft a CSV to give any number of grants, and process it from your local machine.

## Sheet Setup & CSV Format

For data safety, the CSV must adhere to a very specific format.  The first line of the CSV _must_ match the expected headers.  A compatible spreadsheet will look like this:

| Account ID               | Subject                      | Body                     | Banner                   | Icon                       | Note                                   | Expiration | Visible From | Type     | Item          | Quantity | Type     | Item          | Quantity | Type | Item | Quantity |
|:-------------------------|:-----------------------------|:-------------------------|:-------------------------|:---------------------------|:---------------------------------------|:-----------|:-------------|:---------|:--------------|:---------|:---------|:--------------|:---------|:-----|:-----|:---------|
| deadbeefdeadbeefdeadbeef | title_mail_ldr_pvp_bronze_s3 | desc_mail_ldr_pvp_reward | atlas-events[pvp_banner] | atlas-icons[league_bronze] | ldr_beta_preseason_001_20230406 reward |            |              | Currency | soft_currency | 1600     | Currency | soft_currency | 1600     |      |      |

The table as formatted above supports up to 2 attachments per mail message.  You can add more attachments by adding columns in multiples of 3 - with the appropriate headers of `Type`, `Item`, and `Quantity`, in that order.

If the headers do not match what the tool is expecting, the tool will refuse to attempt any mail delivery and exit early.

## Local Tool Setup

Similar to other Platform projects, the grant tool relies on local JSON files to load secrets at runtime.  These secrets should **never** be committed to the repo and be included in the .gitignore file.

To support multiple environments, the tool requires three different JSON files:

* `environment-dev.json`
* `environment-stage.json`
* `environment-prod.json`

Each file needs to be set up with the following information:

```
{
  "MAILBOX_ENDPOINT": "https://{Platform URL}/mail/admin/messages/send",
  "ADMIN_TOKEN": "eyJhb...nR9dA"
}
```

The admin token takes care of Rumble / Game secrets for you.  Ask #platform for admin tokens - which are unique per environment.

### Creating an Admin Token

Refer to token-service's documentation for full details, but it should be noted here that the admin token for this particular tool should only have an audience of `mailbox-service`.  As a local tool, these tokens have some risk of being compromised by malware, and limiting the audience prevents catastrophic damage if we end up in a bad situation.

## Usage

With your environment JSON set up, you only need two arguments; one for the environment and one with a relative file path to your CSV.

```
 Format: ./mailbox-mass-granter -{environment} -{relative path to grant file}
Example: ./mailbox-mass-granter -dev -"bad leaderboards 20230101.csv" 
```

### Example Output

```
/Users/wmaynard/Dev/Rumble/Platform/mailbox-mass-granter/bin/Debug/net7.0/mailbox-mass-granter -dev -"grants.csv"
Reading configuration values from 'environment-dev.json'.
Reading grants from 'grants.csv'...
Validating headers for correctness...
300 messages found to send.  Starting...
.................................................................................................... 100 sent (33 %) | 8928ms elapsed | 17s ETA
.................................................................................................... 200 sent (66 %) | 17362ms elapsed | 8s ETA
....................................................................................................
Success!  All messages were sent, and all the responses were 200-level status codes.  Console output saved to 'grant-log 20230920T145225.txt'.  25841ms total elapsed time.
```

Each successful message sent is represented by a `.`.  Failures are indicated by an `x`.  When failures are encountered, a list of CSV lines that failed will be printed out after all messages have finished, and the log file will include all the information a supporting engineer needs to debug:

```
------------------------------------------------------------------------------------------------------------------------
LINE 6

POST https://dev.nonprod.tower.cdrentertainment.com/mail/admin/messages/send
{
  "accountIds": [ "deadbeefdeadbeefdeadbeef" ],
  "message": {
    "subject": "title_mail_ldr_pvp_bronze_s3",
    "body": "desc_mail_ldr_pvp_reward",
    "expiration": 1697826853,
    "visibleFrom": 1695234853,
    "icon": "atlas-icons[league_bronze]",
    "banner": "atlas-events[pvp_banner]",
    "internalNote": "ldr_beta_preseason_001_20230406 reward",
    "attachments": [
{
  "type": "Currency",
  "rewardId": "soft_currency",
  "quantity": 1500
}
,
{
  "type": "Currency",
  "rewardId": "soft_currency",
  "quantity": 1500
}
]
  }
}

HTTP 400 BadRequest
{"message":"unauthorized","errorCode":"PLATF-0111: TokenValidationFailed","platformData":{"exception":{"details":{"endpoint":null,"code":"TokenValidationFailed","detail":null},"message":"Token is empty or null.","type":"PlatformException","stackTrace":null}}}
------------------------------------------------------------------------------------------------------------------------
```