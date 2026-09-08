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
        const string issueDescription =
            "The Onan generator runs fine for about ten minutes, then shuts off on its own. "
            + "When it quits there's a hot smell near the rear compartment. It will not restart "
            + "for maybe half an hour, then does the same thing again.\n\n"
            + "Happened three times on our last trip. Shore power and the inverter both work "
            + "normally.";
        const string statusUrl = "https://status.rvserviceflow.com/s/abc123";

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
                Phone = "(801) 555-0101",
                Email = "dale@example.com",
                PreferredContact = "Text",
            },
            Origin = new PacketOrigin
            {
                LocationName = "Salt Lake Service Center",
                LocationPhone = "(801) 555-0199",
                SubmittedAtUtc = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.Zero),
                ReferenceCode = "A1B2C3D4",
            },
            IssueCategory = "Electrical / Generator",
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
            PasteBlock = PasteBlockGenerator.Generate("Electrical / Generator", issueDescription, statusUrl),
            StatusLink = new PacketStatusLink { Url = statusUrl },
        };
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
