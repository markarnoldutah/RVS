Subject: Toll-free verification for an ISV sending on behalf of many customer businesses — filing model question before we submit

Hello,

I'm about to file toll-free verification for two Azure Communication Services numbers and I want to file it correctly the first time rather than learn the answer from a rejection five weeks in. The guidelines say verification "is meant for the end business actually sending the message traffic and not for resellers," and that ISVs should fill in the form "on behalf of the customer." Our program has many customers on one number, so I can't tell which way that applies to us.

Our shape

We are RV Intake, a B2B SaaS product for RV dealership service departments. Dealerships pay us a monthly subscription. Our software sends two SMS message types to their customers:

An intake link, sent when a service advisor at the dealership is on the phone with a customer and the customer verbally agrees to receive it. The advisor reads a published consent script that names both the dealership and RV Intake, then ticks a consent box that records the advisor, the timestamp and the number.
A submission confirmation, sent after that customer submits their service request through our web form, where the texting disclosure and a link to our texting terms are shown before submission.
Every message names the dealership it is sent for. We also answer inbound HELP with one fixed reply that names RV Intake and identifies us as sending for the dealership.

The part I need guidance on: one shared toll-free number per environment carries traffic for every dealership we serve, and those dealerships are unrelated businesses, not franchisees of ours. We plan to file one application in RV Intake's name.

Production: +18332398230
Staging (non-production, same program): +18662319618
These sit on two separate Communication Services resources in the same Azure subscription — subscription [ID], resources [names].

Estimated volume is low to start: a pilot of a handful of dealer locations, on the order of [N] messages per month across all of them, growing with the number of subscribing dealerships.

My questions

Can a single application, filed by RV Intake, cover a toll-free number that sends on behalf of many unrelated end businesses? Or does the reviewer expect one verification per end business?
If one application does cover it, whose details belong in Company details — RV Intake's, as the party operating the number and holding the consent records, or a customer dealership's? We have many, so "the customer's" can't be a single answer.
If the application has to name each dealership as a subentity, how do we add one later? The SMS FAQ says brand and campaign updates are currently unavailable and points here. A new subscribing dealership would need adding to an approved verification.
Is there anything equivalent to the Franchise campaign type on a toll-free application? Our shape is close to it — multiple locations sending similar, localized content — though our dealerships are independent businesses rather than franchisees.
Can one application list numbers held by two different Communication Services resources, or do staging and production have to be filed separately?
Why this matters beyond the paperwork

If the answer is that each dealership needs its own verification, then every new customer's onboarding is gated on a 5–8 week carrier process before we can text on their behalf at all. That is a product and go-to-market constraint, not just a filing detail, so I would rather know now than design around the wrong assumption. If that is the answer, I'd also appreciate knowing whether each dealership then needs its own toll-free number, or whether several verifications can attach to one shared number.

Our opt-in evidence is ready — a public texting-terms page, the in-form disclosure, the advisor consent script, and the per-send consent record — so I can submit as soon as I know which model to submit under.

Thank you,

[Legal entity name]
[Name, title]
[Email read daily — status updates go here]
[Phone]