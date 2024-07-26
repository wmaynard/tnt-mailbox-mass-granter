# Mailbox-Service Mass Grant Tool

Occasionally we need to send out rewards packages to players en masse.  Portal has some tooling but is currently intended for individual or small batch processing.  Long-term it would be great to provide mass granting functionality to Portal, but in the meantime, this tool will allow you to craft a CSV to give any number of grants, and process it from your local machine.

You will need to install the .NET Core 7.0 framework to be able to run this tool.

* [macOS Arm64 Installer](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-7.0.401-macos-arm64-installer)
* [Windows x64 Installer](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-7.0.401-windows-x64-installer)
* [Other Versions](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)

## Acknowledgment

The Mail Granter was originally created for Rumble Entertainment (which later became R Studios), a mobile gaming company.  This is a CLI script that was very rough around the edges.  It wasn't optimized or even planned well as it was a quick 2-3 hour scribble requested by Customer Service to send out batches of tens of thousands of in-game mail.  Our internal web application was not able to handle large bulk volume, so letting a console run in the background was a kluge until the web tools were improved to handle the load.  Said update never ended up happening, but this tool was fine being used almost weekly with almost no maintenance or improvements (though async admittedly would have sped things up considerably).

R Studios unfortunately closed its doors in July 2024.  This project has been released as open source with permission.

As of this writing, there may still be existing references to Rumble's resources, such as Confluence links, but their absence doesn't have any significant impact.  Some documentation will also be missing until it can be recreated here, since with the company closure any feature specs and explainer articles originally written for Confluence / Slack channels were lost.

While Rumble is shutting down, I'm grateful for the opportunities and human connections I had working there.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE.txt) file for details.


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
Format: dotnet mailbox-mass-granter.dll -{environment} -{relative path to grant file}
Example: dotnet mailbox-mass-granter.dll -dev -"bad leaderboards 20230101.csv"
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