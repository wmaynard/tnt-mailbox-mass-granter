using System.Text;
using System.Text.Json;

const int INDEX_ACCOUNT_ID = 0;
const int INDEX_SUBJECT = 1;
const int INDEX_BODY = 2;
const int INDEX_BANNER = 3;
const int INDEX_ICON = 4;
const int INDEX_NOTE = 5;
const int INDEX_EXPIRATION = 6;
const int INDEX_VISIBLE_FROM = 7;
const int START_OF_ATTACHMENTS = 8;
string LOG_FILE = $"grant-log {DateTime.Now.ToString("yyyyMMddTHHmmss")}.txt";
string TEMPLATE_PAYLOAD = @"
{
  'accountIds': [ '||accountId||' ],
  'message': {
    'subject': '||subject||',
    'body': '||body||',
    'expiration': ||expiration||,
    'visibleFrom': ||visible||,
    'icon': '||icon||',
    'banner': '||banner||',
    'internalNote': '||note||',
    'attachments': ||attachmentsJson||
  }
}".Replace('\'', '"');
string TEMPLATE_ATTACHMENT = @"
{
  'type': '||type||',
  'rewardId': '||reward||',
  'quantity': ||quantity||
}
".Replace('\'', '"');
string console = "";

#region Helper Methods
string Replace(string input, Dictionary<string, string> replacements) => replacements
    .Aggregate(input, (current, pair) => current.Replace($"||{pair.Key}||", pair.Value));

string CreateAttachment(string type, string rewardId, string quantity) => Replace(TEMPLATE_ATTACHMENT, new Dictionary<string, string>
{
    { "type", type },
    { "reward", rewardId },
    { "quantity", quantity}
});

string CreateMessage(string accountId, string subject, string body, string expiration, string visible, string icon, string banner, string note, params string[] attachments) => Replace(TEMPLATE_PAYLOAD, new Dictionary<string, string>
{
    { "accountId", accountId },
    { "subject", subject },
    { "body", body },
    { "expiration", string.IsNullOrWhiteSpace(expiration) ? $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30 * 24 * 60 * 60}" : expiration },
    { "visible", string.IsNullOrWhiteSpace(visible) ? $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" : visible },
    { "icon", icon },
    { "banner", banner },
    { "note", note },
    { "attachmentsJson", $"[{string.Join(',', attachments)}]"}
});

string CreatePayload(string[] cells)
{
    List<string> attachments = new();
    for (int i = START_OF_ATTACHMENTS; i < cells.Length; i += 3)
    {
        if (string.IsNullOrWhiteSpace(cells[i]))
            break;
        attachments.Add(CreateAttachment(cells[i], cells[i + 1], cells[i + 2]));
    }

    return CreateMessage(
        accountId: cells[INDEX_ACCOUNT_ID],
        subject: cells[INDEX_SUBJECT],
        body: cells[INDEX_BODY],
        expiration: cells[INDEX_EXPIRATION],
        visible: cells[INDEX_VISIBLE_FROM],
        icon: cells[INDEX_ICON],
        banner: cells[INDEX_BANNER],
        note: cells[INDEX_NOTE],
        attachments: attachments.ToArray()
    );
}

long MsSince(DateTime start) => (long)DateTime.Now.Subtract(start).TotalMilliseconds;

void WriteError(string log) => File.AppendAllText(LOG_FILE, log);

void WriteLine(string line = "")
{
    console += $"{line}\n";
    Console.WriteLine(line);
}

void Write(string text)
{
    console += text;
    Console.Write(text);
}

void Exit(int code = 1)
{
    if (File.Exists(LOG_FILE))
        console += $"\n\n{File.ReadAllText(LOG_FILE)}";
    File.WriteAllText(LOG_FILE, console);
    Environment.Exit(code);
}

#endregion Helper Methods

// Find the appropriate environment.json file to use.
string envFile = "environment";
if (args.Contains("-dev"))
    envFile += "-dev.json";
else if (args.Contains("-stage"))
    envFile += "-stage.json";
else if (args.Contains("-prod"))
    envFile += "-prod.json";
else
    envFile += "-dev.json";

// Parse the environment.json file values.
WriteLine($"Reading configuration values from '{envFile}'.");
string json = File.ReadAllText(envFile);
Dictionary<string, string> data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options: new JsonSerializerOptions
{
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip
});

// Verify that the necessary values are present in the environment.json file.
string endpoint = null;
string adminToken = null;
if (!(data.TryGetValue("MAILBOX_ENDPOINT", out endpoint) && data.TryGetValue("ADMIN_TOKEN", out adminToken)))
{
    WriteLine($"Invalid environment values.  Check {envFile} values and try again.");
    Exit(1);
}

// Load the grant CSV.
string grantFile = args.FirstOrDefault(arg => arg.EndsWith(".csv"))?[1..];
WriteLine($"Reading grants from '{grantFile}'...");
if (string.IsNullOrWhiteSpace(grantFile))
{
    WriteLine("No CSV file in arguments.  Expected an argument in the format '-{name}.csv'.");
    Exit(1);
}
string csv = File.ReadAllText(grantFile);
string[] lines = csv.Split(Environment.NewLine);

// Verify all the headers are present.  This helps ensure that our data is in a format we can understand.
WriteLine("Validating headers for correctness...");
string[] headers = lines.First().Split(',');
void VerifyHeader(int position, string expectedText)
{
    if (headers.Length < position)
        throw new Exception($"Invalid header information.  Found {headers.Length} headers, but expected at least {position}.");
    if (headers[position].Replace("\r", "") != expectedText)
        throw new Exception($"Invalid header information.  Expected {expectedText}, got {headers[position]} at position {position}");
}
VerifyHeader(INDEX_ACCOUNT_ID, "Account ID");
VerifyHeader(INDEX_SUBJECT, "Subject");
VerifyHeader(INDEX_BODY, "Body");
VerifyHeader(INDEX_BANNER, "Banner");
VerifyHeader(INDEX_ICON, "Icon");
VerifyHeader(INDEX_NOTE, "Note");
VerifyHeader(INDEX_EXPIRATION, "Expiration");
VerifyHeader(INDEX_VISIBLE_FROM, "Visible From");
for (int i = START_OF_ATTACHMENTS; i < headers.Length; i += 3)
{
    VerifyHeader(i, "Type");
    VerifyHeader(i + 1, "Item");
    VerifyHeader(i + 2, "Quantity");
}

// Get ready to send the mailbox grants
using (HttpClient client = new())
{
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {adminToken}");
    StringContent[] payloads = lines
        .Skip(1)
        .Select(str => str.Replace("\r", "").Split(','))
        .Where(str => str.Any(inner => !string.IsNullOrWhiteSpace(inner)))
        .Select(CreatePayload)
        .Select(payload =>
        {
            try
            {
                // Validate the JSON; if any single line creates invalid JSON, exit the application.
                JsonSerializer.Deserialize<object>(payload);
            }
            catch (JsonException e)
            {
                WriteLine("Invalid JSON; the message will fail.  Check the JSON template in the source code for this tool.");
                WriteLine(payload);
                WriteLine(e.ToString());
                Exit(1);
            }
            
            return new StringContent(payload, Encoding.UTF8, "application/json");
        })
        .ToArray();

    WriteLine($"{payloads.Length} messages found to send.  Starting...");

    List<int> failures = new();
    DateTime start = DateTime.Now;
    
    // Send each of the messages.  At some point in the future, it may be worth exploring a proper bulk
    // endpoint in mailbox to better accomodate this / lower traffic.
    for (int i = 0; i < payloads.Length; i++)
    {
        if (i > 0 && i % 100 == 0)
        {
            long elapsed = MsSince(start);
            double completion = (double) i / (double) payloads.Length;
            int percentComplete = (int)(completion * 100);
            long remaining = (long)(elapsed / completion) - elapsed;

            string minutes = remaining > 60_000 ? $"{(remaining / 60000)}m" : "";
            string seconds = $"{(remaining / 1_000) % 60}s";
            
            WriteLine($" {i} sent ({percentComplete.ToString(),2} %) | {elapsed}ms elapsed | {minutes}{seconds} ETA");
        }

        // Requests will be retried on 3xx or 5xx codes.  4xx means it will never succeed as is.
        int retries = 0;
        bool systemError = false;
        HttpResponseMessage response;
        do
        {
            if (retries > 0)
                Thread.Sleep(millisecondsTimeout: (int)Math.Pow(2, 5 + retries));
            response = await client.PostAsync(
                requestUri: endpoint,
                content: payloads[i]
            );
            int code = (int)response.StatusCode;
            systemError = (code >= 300 && code <= 399) || code >= 500;
        } while (systemError && ++retries < 5);
        

        if (response.IsSuccessStatusCode)
            Write(".");
        else // Print out everything a supporting developer would need to diagnose / retry.
        {
            failures.Add(i + 1);
            Write("x");
            WriteError($@"
------------------------------------------------------------------------------------------------------------------------
LINE {i + 1} {(retries > 0 ? $"(retried {retries} times)" : "")}

POST {endpoint}{await payloads[i].ReadAsStringAsync()}

HTTP {(int)response.StatusCode} {response.StatusCode}
{await response.Content.ReadAsStringAsync()}
------------------------------------------------------------------------------------------------------------------------
");
        }
    }
    WriteLine();

    string totalTime = $"{MsSince(start)}ms total elapsed time.";
    if (failures.Any())
        WriteLine($"WARNING: {failures.Count} messages failed on lines: \n\t{string.Join("\n\t", failures)}\n\nSee '{LOG_FILE}' for more information.  {totalTime}");
    else
        WriteLine($"Success!  All messages were sent, and all the responses were 200-level status codes.  Console output saved to '{LOG_FILE}'.  {totalTime}");
}

Exit(0);