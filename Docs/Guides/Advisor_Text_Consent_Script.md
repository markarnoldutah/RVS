# Before you text a customer the intake link: the consent script

> You'll find **Send intake link** on the manager app's home page, next to your location,
> and beside each store on the **Locations** page. The same script is under **What to say**
> in that window. Read it the same way on every call.

You're on the phone with a customer. Instead of writing down everything they tell you,
you can text them a link. They fill in the details, add photos of the problem, and it
arrives as a one-page write-up. Their name and number are already filled in for them.

**Only send the text after the customer has said yes to getting it.** That's the rule, and
it's the law. The words below are how you ask.

---

## What to say

Read this, or say it in your own words as long as every point is covered:

> "I can text you a link so you can send us the details and some photos of the problem.
> It's one text from **[your store name]**, sent through our RV Intake service, to this
> number: **[read the number back]**. It's just the one message with a link — you'd only
> get more texts if you ask for text updates on the form. We won't use it for marketing.
> Standard message and data rates may apply, and you can reply STOP any time to stop
> texts, or HELP for help.
> Is it OK if I send that now?"

**Say "through our RV Intake service."** The text arrives from a number the customer
doesn't recognise, shared by every store that uses RV Intake. Naming the service on the
call is what makes that number expected rather than suspicious — and the carriers who
approve our texting number require the customer to be told who is really sending.

Then **wait for a clear yes**. "Yes", "Sure", "Go ahead" and "Please do" all count.

## What counts, and what doesn't

| Customer says | What you do |
|---|---|
| A clear yes | Tick the consent box and tap **Send** |
| "Uh, I guess?" or anything unsure | Ask once more: *"Just to check, OK to text you the link?"* Send only on a clear yes |
| No | **Don't send.** Use **Fill it in myself** and take the details on the call |
| "Just email me" | Don't text. Choose **Email** instead (see below) |
| "Send it to my wife's phone" | Only if she's on the call and says yes herself. Otherwise, text the caller |
| No answer, or the call dropped | **Don't send.** A missed call is not a yes |

Tick the consent box **only after** you hear the yes. It records who asked and when. Don't
tick it ahead of time to save a step.

## What the customer gets

One text from the store's texting number, which is a toll-free number, not your cell. It
looks like this:

> **[Store name]:** Hi **[first name]**, here's the link to start your service request:
> go.rvintake.com/your-store-name?src=advisor&inv=…
> Msg & data rates may apply. Reply STOP to opt out, HELP for help.

The link works once and expires after about 3 days. If they lose it or it runs out, find
them under **Sent this shift** and tap **Resend**. Their name and number go back in the
form. Tick the box and tap **Send** for a new link. You don't need to ask again if you're
still on the same call. If it's a later call, ask again first.

## If they've opted out before

If this number has replied STOP to us before, the app won't send, and it will tell you so.
Don't try to get around it. Use **Fill it in myself**, or ask for an email address instead.

## If they'd rather have an email

Some customers would rather get the link by email. In **Send intake link**, choose
**Email**, type their email address and read it back to them. Tick the box once they've
said yes, then tap **Send**. There's no script to read for an email. It's the same link, it
comes from RV Intake on the store's behalf, and it works once for about 3 days. The app
shows **Emailed** once it's gone out. Unlike a text, it can't tell you whether the email
was delivered, so if they say it hasn't arrived, ask them to check their junk folder before
you resend.

If the app says the address has opted out of email, don't send. Use **Fill it in myself**.

## If texting is switched off

Sometimes the app says **texting is not yet enabled**. That means the texting number is
still waiting for approval from the phone carriers. Email and **Fill it in myself** still work.

---

## For RVS staff

This page is the advisor-facing opt-in script for Spec A-14. It is also the opt-in flow
described in the toll-free verification application for each environment's sending number
(#659), so change it only in step with that application and the Spec. The consent record
in the manager app stores the advisor, the time the box was ticked and the number, and it
never expires.

**The sample text above is generated, not written here.** It comes from
`IntakeInviteContent.BuildSmsBody`, and the compliance tail — *Msg & data rates may
apply. Reply STOP to opt out, HELP for help.* — is pinned by a test in
`IntakeInviteContentTests`, because the same wording is submitted as a sample message on
the verification application. Three things move together or not at all: that constant,
this page, and the application. The script is also copied into the manager app's **What
to say** panel (`SendIntakeLinkDialog.razor`), so a wording change to the script touches
that file too. The customer-facing terms are at `/sms-terms`
(`rvintake.com/sms-terms`), which the application cites as the opt-in proof URL.
