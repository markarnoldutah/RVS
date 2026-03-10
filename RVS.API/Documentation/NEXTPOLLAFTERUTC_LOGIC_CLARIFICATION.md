# NextPollAfterUtc - Logic Clarification

## ?? Important: Understanding the Property Name

### What `NextPollAfterUtc` Means

The property name **`NextPollAfterUtc`** means:
> "The next poll should happen **AFTER** this time (UTC)"

Or more precisely:
> "Don't poll **UNTIL AFTER** this time has passed"

---

## ? Correct Polling Logic

### The Rule

```csharp
var shouldPoll = (check.NextPollAfterUtc == null) ||           // No time restriction
                 (DateTime.UtcNow >= check.NextPollAfterUtc);  // Wait time has passed
```

### Why This Makes Sense

| Condition | Meaning | Action |
|-----------|---------|--------|
| `NextPollAfterUtc == null` | No time restriction | ? Poll immediately |
| `DateTime.UtcNow >= NextPollAfterUtc` | Current time is at or past the wait time | ? Poll now |
| `DateTime.UtcNow < NextPollAfterUtc` | Current time is before the wait time | ?? Don't poll yet |

---

## ?? Real-World Example

Suppose Availity says "check back after 10:30 AM":

```csharp
check.NextPollAfterUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
```

### Timeline of Poll Decisions

```
Current Time          | NextPollAfterUtc  | Comparison          | Decision
?????????????????????????????????????????????????????????????????????????????
10:28:00 UTC         | 10:30:00 UTC      | 10:28 < 10:30       | ?? WAIT (2 min)
10:29:00 UTC         | 10:30:00 UTC      | 10:29 < 10:30       | ?? WAIT (1 min)
10:30:00 UTC         | 10:30:00 UTC      | 10:30 >= 10:30 ?    | ? POLL NOW
10:30:05 UTC         | 10:30:00 UTC      | 10:30:05 >= 10:30 ? | ? POLL NOW
10:31:00 UTC         | 10:30:00 UTC      | 10:31 >= 10:30 ?    | ? POLL NOW
```

---

## ?? Why the Confusion?

### Property Name Could Be Interpreted Two Ways

1. **"After this time"** (Correct Interpretation) ?
   - Means: "Poll after this time has passed"
   - Logic: `DateTime.UtcNow >= NextPollAfterUtc` (current time is at or past the threshold)

2. **"After current time"** (Incorrect Interpretation) ?
   - Means: "Poll after current time"
   - Logic: `NextPollAfterUtc > DateTime.UtcNow` (threshold is in future)

### Better Alternative Names

If we could rename this property, clearer options would be:

1. **`CanPollAfterUtc`** - "You CAN poll after this time"
2. **`DoNotPollBeforeUtc`** - "Do NOT poll before this time"
3. **`EarliestNextPollUtc`** - "Earliest time for next poll"
4. **`PollNotBeforeUtc`** - Negative framing (clearer intent)

---

## ?? Code Examples

### ? Correct Client-Side Implementation

```csharp
// Blazor WASM Polling Service
private List<string> GetChecksReadyToPoll(
    List<EligibilityCheckSummaryResponseDto> checks)
{
    var now = DateTime.UtcNow;
    
    return checks
        .Where(c => c.Status == "InProgress")
        .Where(c => c.NextPollAfterUtc == null ||       // No restriction
                    now >= c.NextPollAfterUtc)           // Wait time passed
        .Select(c => c.EligibilityCheckId)
        .ToList();
}
```

### ? Correct TypeScript/JavaScript Implementation

```typescript
function getChecksReadyToPoll(
    checks: EligibilityCheckSummary[]
): string[] {
    const now = new Date();
    
    return checks
        .filter(c => c.status === 'InProgress')
        .filter(c => !c.nextPollAfterUtc ||                           // No restriction
                     now >= new Date(c.nextPollAfterUtc))             // Wait time passed
        .map(c => c.eligibilityCheckId);
}
```

---

## ?? Server-Side Logic (How It's Set)

### When Availity Responds

```csharp
// In EligibilityCheckService.cs - PollAndUpdateAsync method
if (response.IsProcessing)
{
    // Still in progress
    check.Status = "InProgress";
    
    // Set next poll time based on Availity's ETA or default interval
    check.NextPollAfterUtc = response.EtaDate ?? 
                             DateTime.UtcNow.AddMilliseconds(DefaultPollIntervalMs);
}
```

### What This Means

If Availity says **"Check back at 10:30 AM"**:
- Server sets: `NextPollAfterUtc = 10:30:00 UTC`
- Client interprets: "Don't poll until 10:30 AM or later"
- At 10:29 AM: Client waits (saves 1 unnecessary poll = ~2.5 RU)
- At 10:30 AM: Client polls

---

## ?? Why This Optimization Matters

### Without Time-Based Filtering

```
Every 2 seconds, poll ALL InProgress checks:
- 10:28:00: Poll check ? "Still processing, try after 10:30"  (wasted 2.5 RU)
- 10:28:02: Poll check ? "Still processing, try after 10:30"  (wasted 2.5 RU)
- 10:28:04: Poll check ? "Still processing, try after 10:30"  (wasted 2.5 RU)
... (60 wasted polls in 2 minutes = 150 RU wasted!)
```

### With Time-Based Filtering

```
Check NextPollAfterUtc before polling:
- 10:28:00: DateTime.UtcNow (10:28) < NextPollAfterUtc (10:30) ? SKIP  (saved 2.5 RU)
- 10:28:02: DateTime.UtcNow (10:28) < NextPollAfterUtc (10:30) ? SKIP  (saved 2.5 RU)
... (all polls skipped until 10:30)
- 10:30:00: DateTime.UtcNow (10:30) >= NextPollAfterUtc (10:30) ? POLL ?  (2.5 RU)
- 10:30:02: Check complete ? STOP
Total: 2.5 RU (vs 150 RU without filtering)
Savings: 147.5 RU (98% reduction!)
```

---

## ?? Unit Test Example

```csharp
[Fact]
public void GetChecksReadyToPoll_RespectsNextPollAfterUtc()
{
    // Arrange
    var now = DateTime.UtcNow;
    var checks = new List<EligibilityCheckSummaryResponseDto>
    {
        new() 
        { 
            EligibilityCheckId = "check1", 
            Status = "InProgress",
            NextPollAfterUtc = now.AddMinutes(-1)  // In the past
        },
        new() 
        { 
            EligibilityCheckId = "check2", 
            Status = "InProgress",
            NextPollAfterUtc = now.AddMinutes(1)   // In the future
        },
        new() 
        { 
            EligibilityCheckId = "check3", 
            Status = "InProgress",
            NextPollAfterUtc = null                // No restriction
        },
        new() 
        { 
            EligibilityCheckId = "check4", 
            Status = "Complete",                   // Terminal
            NextPollAfterUtc = now.AddMinutes(-1)
        }
    };
    
    // Act
    var readyIds = GetChecksReadyToPoll(checks);
    
    // Assert
    Assert.Equal(2, readyIds.Count);
    Assert.Contains("check1", readyIds);  // Past time ? poll
    Assert.Contains("check3", readyIds);  // No restriction ? poll
    Assert.DoesNotContain("check2", readyIds);  // Future time ? wait
    Assert.DoesNotContain("check4", readyIds);  // Terminal ? skip
}
```

---

## ?? Summary

### The Golden Rule

**Poll when current time is at or past `NextPollAfterUtc`**

```csharp
bool shouldPoll = (check.NextPollAfterUtc == null) ||           // No time restriction
                  (DateTime.UtcNow >= check.NextPollAfterUtc);  // Wait time has passed
```

### Memory Aid

Think of `NextPollAfterUtc` as a **"DO NOT DISTURB UNTIL"** sign:
- If the sign says "Do not disturb until 10:30 AM"
- At 10:29 AM ? respect the sign, don't disturb (don't poll)
- At 10:30 AM ? sign time has passed, you can disturb (poll now)

**Read the comparison naturally:**  
"Is the current time greater than or equal to the next poll time?"  
`DateTime.UtcNow >= check.NextPollAfterUtc`

---

**Last Updated**: 2025-01-15  
**Status**: ? Logic Verified & Documented
