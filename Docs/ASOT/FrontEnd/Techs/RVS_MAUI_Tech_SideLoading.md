# MAUI.Tech — Side-Loading & Distribution Options

> **Purpose:** Investigate side-loading options for `RVS.MAUI.Tech` iOS and Android apps and compare with public app store distribution. Informs the distribution strategy for MVP design-partner onboarding and post-MVP scaling.

## Context

`RVS.MAUI.Tech` is a MAUI Blazor Hybrid app (iOS + Android) used by **dealership technicians** in service bays. Key distribution constraints:

- **Employer-provisioned** — technicians do not self-discover the app; dealerships install it on company or BYOD devices.
- **Small initial audience** — 5 design-partner dealerships, roughly 5–20 technicians each (25–100 devices at MVP).
- **No consumer app store presence needed** — the app is not customer-facing.
- **Frequent iteration** — MVP phase requires rapid update cycles without app store review delays.
- **Security** — the app handles service-request data with tenant isolation; APKs/IPAs should not leak publicly.

Cross-reference: [RVS Implementation Plan §9.1](../../RVS_implementation_plan.md) and [RVS Context §2.4](../../RVS_Context.md).

---

## iOS Distribution Options

### Ad Hoc Distribution

| Attribute | Detail |
|---|---|
| **Mechanism** | Build `.ipa` with Ad Hoc provisioning profile; distribute via email, cloud storage, or MDM |
| **Apple Developer Program** | Standard ($99/year) |
| **Device limit** | 100 per device type (iPhone, iPad) per year |
| **App Review** | None |
| **Build expiration** | 1 year (provisioning profile expiry) |
| **Update process** | Manual — re-sign and redistribute `.ipa` for each update |
| **Best for** | Internal pilot with known devices; quick prototyping |

**Pros:**

- No Apple review — fastest path from build to device
- Zero infrastructure beyond a file share or CI artifact
- Good for early MVP testing with <100 devices

**Cons:**

- 100-device hard cap per device type per year — does not scale past MVP
- Every target device UDID must be registered in the provisioning profile
- No automatic updates — technicians must manually install each new build
- Profile expires yearly; app stops working until re-signed

### TestFlight

| Attribute | Detail |
|---|---|
| **Mechanism** | Upload build to App Store Connect; invite testers via email or public link |
| **Apple Developer Program** | Standard ($99/year) |
| **Device limit** | 100 internal testers (no review), 10,000 external testers (light review) |
| **App Review** | Internal: none. External: abbreviated beta review (typically <24 hours) |
| **Build expiration** | 90 days per build |
| **Update process** | Upload new build → testers notified automatically via TestFlight app |
| **Best for** | Pre-release beta testing; design-partner onboarding |

**Pros:**

- Standard Apple-supported beta channel — familiar to testers
- Automatic update notifications via TestFlight app
- Up to 10,000 external testers — scales well beyond MVP
- No UDID management for external testers
- Crash reporting and feedback built in

**Cons:**

- 90-day build expiry — must upload a fresh build at least quarterly
- External testers require a light Apple review per build
- TestFlight app must be installed on tester devices
- Not suitable for permanent production deployment

### Apple Business Manager Custom Apps

| Attribute | Detail |
|---|---|
| **Mechanism** | Upload to App Store Connect as a Custom App; target specific organizations via Apple Business Manager (ABM) |
| **Apple Developer Program** | Standard ($99/year) + receiving org needs ABM enrollment |
| **Device limit** | Unlimited within targeted organizations |
| **App Review** | Full App Store review (app is private but still reviewed) |
| **Build expiration** | None — persists like a public App Store app |
| **Update process** | Submit update through App Store Connect; MDM pushes to devices |
| **Best for** | Post-MVP production distribution to dealer organizations |

**Pros:**

- Unlimited devices within enrolled organizations
- Silent install/update via MDM — zero technician interaction
- Full app lifecycle management (revocation, compliance)
- Apple's recommended replacement for the Enterprise Program
- Standard $99/year developer cost

**Cons:**

- Each dealership must enroll in Apple Business Manager (free, but requires D-U-N-S number)
- Full App Store review for each release — adds 1–7 day review cycle
- More setup overhead than TestFlight
- Requires dealership IT cooperation for ABM enrollment

### Apple Developer Enterprise Program

| Attribute | Detail |
|---|---|
| **Mechanism** | Sign with Enterprise Certificate; distribute `.ipa` via internal channels or MDM |
| **Apple Developer Program** | Enterprise ($299/year) |
| **Device limit** | Unlimited (employees of the enrolled organization only) |
| **App Review** | None |
| **Build expiration** | 1 year (certificate renewal) |
| **Update process** | Manual or MDM-pushed |
| **Best for** | Large organizations distributing to internal employees only |

**Pros:**

- No App Store review
- Unlimited internal devices
- Full control over distribution

**Cons:**

- $299/year — 3× standard program cost
- **Apple is deprecating this pathway** in favor of ABM Custom Apps
- Strictly for internal employees — distributing to partner dealerships violates Apple's terms
- Not appropriate for RVS (multi-tenant SaaS distributed to many independent dealership organizations)

### App Store (Public)

| Attribute | Detail |
|---|---|
| **Mechanism** | Submit to App Store via App Store Connect; available to the public |
| **Apple Developer Program** | Standard ($99/year) |
| **Device limit** | Unlimited |
| **App Review** | Full review (1–7 days typical) |
| **Build expiration** | None |
| **Update process** | Submit update → review → auto-update for users |
| **Best for** | Consumer apps or wide public availability |

**Pros:**

- Broadest reach — any iOS user can install
- Auto-updates via App Store
- Trust and credibility from App Store presence
- Built-in discovery, ratings, and reviews

**Cons:**

- Full Apple review for every release — slows iteration
- App is visible to the public (not private)
- Must meet all App Store guidelines (UI, content, privacy)
- Overkill for an employer-provisioned technician tool

---

## Android Distribution Options

### Direct APK/AAB Sideload

| Attribute | Detail |
|---|---|
| **Mechanism** | Build APK or AAB; distribute via email, cloud storage, or direct download |
| **Google account** | None required for distribution |
| **Device limit** | Unlimited |
| **App Review** | None |
| **Update process** | Manual — user downloads and installs each update |
| **Best for** | Developer testing; quick prototyping |

**Pros:**

- Fastest path from build to device — zero infrastructure
- No Google account or Play Console needed
- Complete control over distribution
- Useful for CI artifact distribution during development

**Cons:**

- Users must enable "Install unknown apps" — reduced security posture
- No automatic updates — manual reinstall for each version
- APK files can be intercepted or redistributed if not secured
- No crash reporting or analytics
- **New in 2026:** Google is introducing developer verification for off-Play installs — Managed Google Play and MDM-deployed apps are exempt

### Google Play Internal Testing Track

| Attribute | Detail |
|---|---|
| **Mechanism** | Upload to Google Play Console; add testers by email to internal testing track |
| **Google account** | Developer account ($25 one-time) |
| **Device limit** | 100 internal testers |
| **App Review** | Automated malware/compatibility scan (no manual review for internal track) |
| **Update process** | Upload new build → testers receive update via Play Store |
| **Best for** | Pre-release testing with controlled tester groups |

**Pros:**

- Standard Play Store install and auto-update experience
- Built-in crash reporting, analytics, and pre-launch checks
- No sideloading required — higher security
- Fast publishing (minutes, not days)
- Can promote to closed/open beta → production when ready

**Cons:**

- Testers need Google accounts
- 100-tester limit on internal track (closed track supports thousands)
- Requires Google Play Console setup and asset listing
- App must meet Play Store technical requirements

### Google Play Closed/Open Testing Tracks

| Attribute | Detail |
|---|---|
| **Mechanism** | Upload to Play Console; invite testers via email lists or opt-in link |
| **Google account** | Developer account ($25 one-time) |
| **Device limit** | Thousands (closed) to unlimited (open) |
| **App Review** | Automated scan; manual review may apply for open track |
| **Update process** | Auto-update via Play Store |
| **Best for** | Broader beta testing; staged rollout before production |

**Pros:**

- Scales beyond internal testing limits
- Play Store distribution and auto-update
- Graduated promotion path: internal → closed → open → production

**Cons:**

- Open track makes app discoverable — less private
- Closed track management overhead for multi-dealership groups
- Still requires Play Store compliance

### Managed Google Play (Private Apps via MDM)

| Attribute | Detail |
|---|---|
| **Mechanism** | Upload APK/AAB to Managed Google Play via Play Console or MDM admin console; deploy to managed devices |
| **Google account** | Managed Google Play organization enrollment (free with Google Workspace or standalone) |
| **Device limit** | Unlimited within managed organization(s); can target up to 1,000 organizations |
| **App Review** | Automated scan only — no manual review |
| **Update process** | Silent push via MDM — zero technician interaction |
| **Best for** | Post-MVP production distribution; enterprise-managed devices |

**Pros:**

- Silent install and update — best technician experience
- Strong device, app, and user management via MDM (e.g., Microsoft Intune)
- Exempt from 2026 developer verification requirements for off-Play installs
- Private — not visible on public Play Store
- Enforce security policies, remote wipe, compliance checks
- AAB support rolling out for private apps (2025–2026)

**Cons:**

- Requires MDM/EMM infrastructure (e.g., Intune, Workspace ONE)
- Each dealership needs managed device enrollment
- Setup complexity higher than sideloading
- Requires ongoing MDM license costs
- Devices must be enrolled and managed

### Google Play Store (Public)

| Attribute | Detail |
|---|---|
| **Mechanism** | Publish to Google Play Store via Play Console |
| **Google account** | Developer account ($25 one-time) |
| **Device limit** | Unlimited |
| **App Review** | Automated scan + manual review |
| **Update process** | Auto-update via Play Store |
| **Best for** | Consumer apps or wide public availability |

**Pros:**

- Broadest reach — any Android user can install
- Auto-updates, crash reporting, analytics
- Trust from Play Store presence
- Built-in discovery and ratings

**Cons:**

- Manual review can delay releases
- App is publicly visible
- Must meet all Play Store policies
- Unnecessary overhead for an internal technician tool

---

## Side-by-Side Comparison

### iOS

| Criteria | Ad Hoc | TestFlight | ABM Custom Apps | Enterprise Program | App Store |
|---|---|---|---|---|---|
| **MVP suitability** | ✅ Good | ✅ Best | ⚠️ Over-engineered | ❌ Wrong fit | ❌ Overkill |
| **Post-MVP suitability** | ❌ Doesn't scale | ⚠️ 90-day expiry | ✅ Best | ❌ Deprecated | ⚠️ Unnecessary |
| **Device limit** | 100/type/year | 10,000 | Unlimited | Unlimited | Unlimited |
| **Apple review** | None | Light (external) | Full | None | Full |
| **Auto-update** | ❌ | ✅ | ✅ (via MDM) | ❌ | ✅ |
| **Setup complexity** | Low | Low | Medium–High | Medium | Low |
| **Annual cost** | $99 | $99 | $99 + ABM setup | $299 | $99 |
| **Privacy** | Private | Private | Private | Private | Public |

### Android

| Criteria | APK Sideload | Play Internal | Play Closed/Open | Managed Google Play | Play Store |
|---|---|---|---|---|---|
| **MVP suitability** | ✅ Good | ✅ Best | ✅ Good | ⚠️ Over-engineered | ❌ Overkill |
| **Post-MVP suitability** | ❌ Doesn't scale | ⚠️ 100-tester cap | ✅ Good | ✅ Best | ⚠️ Unnecessary |
| **Device limit** | Unlimited | 100 | Thousands+ | Unlimited | Unlimited |
| **Review** | None | Automated only | Automated/Manual | Automated only | Automated + Manual |
| **Auto-update** | ❌ | ✅ | ✅ | ✅ (silent) | ✅ |
| **Setup complexity** | Low | Low | Low–Medium | Medium–High | Low |
| **Cost** | Free | $25 (one-time) | $25 (one-time) | $25 + MDM license | $25 (one-time) |
| **Privacy** | Private | Private | Semi-private | Private | Public |

---

## Recommendation for RVS

### MVP Phase (Design Partners, 25–100 devices)

| Platform | Recommended Channel | Rationale |
|---|---|---|
| **iOS** | **TestFlight** | Zero UDID management, auto-update notifications, supports 10,000 testers, built-in crash reporting. Light review is fast (<24 hours). Aligns with existing implementation plan. |
| **Android** | **Google Play Internal Testing** | Standard Play Store experience, auto-updates, built-in analytics. 100-tester limit is sufficient for 5 design partners. No sideloading security concerns. |

**CI/CD integration:** The `build-mobile.yml` GitHub Actions workflow (triggered by `mobile-v*` tags) builds APK + IPA artifacts. For MVP:

1. Upload IPA to App Store Connect → TestFlight automatically distributes to invited testers
2. Upload AAB to Play Console Internal Testing track → testers receive update via Play Store

### Post-MVP Scaling (50+ dealerships, 500+ devices)

| Platform | Recommended Channel | Rationale |
|---|---|---|
| **iOS** | **Apple Business Manager Custom Apps** | Unlimited devices, MDM silent install/update, enterprise-grade management. Each dealership enrolls in ABM (free). Full review cycle is acceptable for stable production releases. |
| **Android** | **Managed Google Play (Private Apps via MDM)** | Silent push updates, strong compliance/security, exempt from 2026 verification rules. Works with Microsoft Intune (already in Azure ecosystem). Scales to 1,000 organizations. |

**Transition path:**

1. MVP: TestFlight (iOS) + Play Internal Testing (Android)
2. Growth: Add ABM Custom Apps (iOS) + Managed Google Play (Android) as dealerships adopt MDM
3. TestFlight can continue alongside ABM for pre-release beta builds

### Why Not Public App Stores?

The public App Store and Google Play Store are **not recommended** for `RVS.MAUI.Tech` because:

- **No consumer audience** — technicians are employer-provisioned, not self-discovering the app
- **Review delays** — full app store reviews (1–7 days) slow down MVP iteration cycles
- **Public visibility** — unnecessary exposure of an internal enterprise tool
- **Compliance overhead** — app store guidelines add requirements (privacy policies, screenshots, metadata) with no business benefit
- **No competitive advantage** — app store presence provides no value for a B2B employer-provisioned tool

Public store listing may be reconsidered only if RVS pivots to a model where individual technicians self-enroll (not currently planned per [RVS PRD §7.5](../../RVS_PRD.md)).

---

## MDM Considerations for Post-MVP

For the ABM Custom Apps + Managed Google Play strategy, dealerships need MDM enrollment. Recommended approach:

- **Microsoft Intune** — natural fit given RVS's Azure ecosystem; supports both iOS (ABM) and Android (Managed Google Play)
- **Enrollment models:**
  - **Corporate-owned, fully managed** — dealership provides devices; Intune controls everything
  - **BYOD with work profile** — technician's personal device; work data isolated in managed profile
- **RVS onboarding checklist addition:** Add MDM enrollment as a step in the design-partner onboarding process (Phase 9.3) when dealerships graduate from TestFlight/Internal Testing to production distribution

---

## GitHub Actions Build Pipeline

The existing `build-mobile.yml` workflow produces APK and IPA artifacts. To support the recommended distribution channels:

| Build artifact | MVP distribution | Post-MVP distribution |
|---|---|---|
| **IPA** | Upload to App Store Connect via `altool` or Transporter CLI → TestFlight | Same upload path → ABM Custom Apps |
| **AAB** | Upload to Play Console via `google-play` GitHub Action → Internal Testing track | Same upload path → Managed Google Play private app |

Both channels use the same App Store Connect and Play Console upload pipeline — the only difference is the distribution target configuration. This means **zero CI/CD changes** when transitioning from MVP to post-MVP distribution.
