using RVS.Domain.Packets;

namespace RVS.PacketDump;

/// <summary>
/// Representative <see cref="ServicePacket"/> instances for eyeballing and printing the
/// HTML renderer's output. These mirror what <see cref="PacketComposer"/> produces; they
/// are not test fixtures and carry no assertions.
/// </summary>
internal static class SamplePackets
{
    /// <summary>Every section populated — the packet at its longest.</summary>
    public static ServicePacket Full()
    {
        // What the customer dictated, before curation — the "Complaint — word for word" block.
        const string issueDescription =
            "um so the the onan generator it runs okay for like about ten minutes and then it "
            + "just uh shuts itself off, and when it quits theres this real hot smell kinda near "
            + "the back compartment there. it wont start back up for i dunno maybe half an hour "
            + "and then it just does the same thing over again.\n\n"
            + "it happened uh three times on our last trip. the shore power and the inverter "
            + "those both work fine.";

        // The curated restatement, rendered above the assessment as "Issue".
        const string curatedIssue =
            "Onan generator runs about ten minutes, then shuts off on its own, with a hot smell "
            + "near the rear compartment. Will not restart for roughly half an hour, then repeats. "
            + "Occurred three times on the last trip. Shore power and inverter both unaffected.";
        const string statusUrl = "https://rvintake.com/status/abc123";

        return new()
        {
            Unit = new PacketUnitHeader
            {
                Year = 2021,
                Make = "Winnebago",
                Model = "View 24D",
                Vin = "1FDXE45S12HB00001",
            },
            Customer = new PacketCustomer
            {
                FullName = "Dale Gribble",
                FirstName = "Dale",
                LastName = "Gribble",
                Phone = "(801) 555-0101",
                Email = "dale@example.com",
                PreferredContact = "Text",
            },
            Origin = new PacketOrigin
            {
                LocationName = "Salt Lake Service Center",
                LocationPhone = "(801) 555-0199",
                // Set so a print test exercises the dealership-local Received line (issue
                // #506); Minimal() is left zone-less so the UTC fallback is printed too.
                LocationTimeZoneId = "America/Denver",
                SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
                ReferenceCode = "A1B2C3D4",
            },
            IssueCategory = "Electrical / Generator",
            CuratedIssue = curatedIssue,
            IssueDescription = issueDescription,
            Diagnostics =
            [
                new PacketDiagnosticEntry
                {
                    Question = "Does the generator start at all?",
                    Answers = ["Yes, then dies", "Dies after about 10 minutes of runtime"],
                },
                new PacketDiagnosticEntry
                {
                    Question = "Any warning lights or error codes on the generator panel?",
                    Answers = ["Red temperature light comes on right before it quits"],
                },
                new PacketDiagnosticEntry
                {
                    Question = "When did this start?",
                    Answers = ["Within the last month"],
                },
                new PacketDiagnosticEntry
                {
                    Question = "What have you already tried?",
                    Answers = ["Reset the breaker", "Checked the fuel level", "Let it cool and restarted"],
                },
            ],
            AiSummary = new PacketAiSummary
            {
                Text =
                    "Customer reports the Onan generator runs ~10 minutes then shuts down with an "
                    + "over-temperature light and a hot smell from the rear compartment; ~30 minute "
                    + "cool-down before it restarts, then repeats. Shore power and inverter "
                    + "unaffected. Pattern is consistent with an airflow/cooling restriction or a "
                    + "failing temperature sensor causing a thermal shutdown.",
                ProbableCause =
                    "Thermal shutdown from restricted cooling airflow; a failing temperature sensor would show the same pattern.",
                PossibleFixes =
                [
                    "Clear debris from the generator air intake, cooling fins, and compartment vents",
                    "Test the temperature sensor and high-temp shutdown switch, and replace if out of range",
                    "Check the cooling fan and its belt or coupling",
                ],
                LikelyParts = ["Air filter", "Temperature sensor", "High-temp shutdown switch"],
                Confidence = "Medium",
                // Issue #772: what the assessment read off the photos, cited to them.
                PhotoFindings =
                [
                    new PacketPhotoFinding { Text = "Generator — Onan 5500 · S/N K190123456", PhotoLabel = "photo 1, gen-rear.jpg" },
                    new PacketPhotoFinding { Text = "Generator — code 36: Engine stopped", PhotoLabel = "photo 2, gen-panel.jpg" },
                    new PacketPhotoFinding { Text = "Debris packed against the compartment intake vent", PhotoLabel = "photo 1, gen-rear.jpg" },
                ],
            },
            Photos =
            [
                new PacketPhoto
                {
                    Url = "https://example.blob.core.windows.net/att/gen-rear.jpg?sv=2024&sig=aaa%2Bbbb",
                    FileName = "gen-rear.jpg",
                    Caption = "Rear compartment",
                },
                new PacketPhoto
                {
                    Url = "https://example.blob.core.windows.net/att/gen-panel.jpg?sv=2024&sig=ccc%2Bddd",
                    FileName = "gen-panel.jpg",
                    Caption = "Generator panel with temp light",
                },
            ],
            PasteBlock = PasteBlockGenerator.Generate(
                "Electrical / Generator",
                issueDescription,
                statusUrl,
                equipmentLines: ["EQUIPMENT: Generator — Onan 5500 · S/N K190123456", "FAULT CODE: Generator — code 36: Engine stopped"]),
            StatusLink = new PacketStatusLink { Url = statusUrl },
            // The footer's "Powered by" mark as production serves it (issue #470). The minimal
            // sample leaves it unset to show the text fallback.
            Branding = new PacketBranding { PoweredByLogoUrl = PacketBranding.PoweredByLogoUrlFor("https://rvintake.com") },
        };
    }

    /// <summary>
    /// A three-issue visit (<c>Spec A-17</c>, issue #806), rendered as one packet through
    /// <see cref="PacketHtmlRenderer.RenderCombined"/>: the full sample leads and supplies the
    /// masthead, then two shorter issues. Each carries manager links so the per-issue status
    /// buttons show in the HTML.
    /// </summary>
    public static IReadOnlyList<ServicePacket> Multi()
    {
        const string managerBaseUrl = "https://manager.rvintake.com";
        var lead = Full();

        ServicePacket Issue(string id, string reference, string category, string description, string? curated) => lead with
        {
            Origin = lead.Origin with { ReferenceCode = reference },
            IssueCategory = category,
            CuratedIssue = curated,
            AiSummary = null,
            IssueDescription = description,
            Diagnostics =
            [
                new PacketDiagnosticEntry { Question = "When did you first notice it?", Answers = ["This week"] },
            ],
            Photos = [],
            PasteBlock = PasteBlockGenerator.Generate(category, description, lead.StatusLink?.Url),
            ManagerLinks = ManagerDeepLinks.Build(managerBaseUrl, id),
        };

        return
        [
            lead with { ManagerLinks = ManagerDeepLinks.Build(managerBaseUrl, "a1b2c3d4-0000-0000-0000-000000000001") },
            Issue(
                "b7c8d9e0-0000-0000-0000-000000000002", "B7C8D9E0", "Appliances & Refrigerator",
                "fridge isnt getting cold on propane, works fine on shore power",
                "Refrigerator cools on shore power but not on LP."),
            Issue(
                "c3d4e5f6-0000-0000-0000-000000000003", "C3D4E5F6", "Awning",
                "the awning fabric has a tear near the roller about a foot long",
                null),
        ];
    }

    /// <summary>
    /// The maximally degraded packet the composer can still emit: no VIN, no category,
    /// no diagnostics, no AI summary, no photos, no paste block, no status link.
    /// </summary>
    public static ServicePacket Minimal() => new()
    {
        Unit = new PacketUnitHeader(),
        Customer = new PacketCustomer { FullName = "Jane Doe" },
        Origin = new PacketOrigin
        {
            SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
            ReferenceCode = "DEADBEEF",
        },
        IssueDescription = "Slide-out makes a grinding noise about halfway out and then stops.",
        Diagnostics = [],
        Photos = [],
    };
}
