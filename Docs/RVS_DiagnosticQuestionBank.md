# Fallback Diagnostic Question Bank

**Spec A-4.** These are the hand-written diagnostic follow-up questions shown to a customer
during intake **when the AI question generator fails or is not configured**. One set per
issue category in [`IssueCategoryVocabulary`](../RVS.Domain/Validation/IssueCategoryVocabulary.cs).
Today the AI endpoint is unset in every environment, so in practice this bank *is* the
diagnostic Q&A the packet carries.

- **Source of truth:** [`RuleBasedCategorizationService.cs`](../RVS.API/Integrations/RuleBasedCategorizationService.cs)
  (`DiagnosticQuestions`). This document is a readable mirror — if the two disagree, the code wins.
- **Status:** draft, pending review with a working RV technician — issue #511, section 2.
- **Contract each question obeys:** 2–4 questions per category; each question has 0 options
  (pure free text) or 2–6 mutually-exclusive options; free text is always allowed; help text
  only where a non-expert owner needs it.

## How to review this (for the tech)

For **each category** below, and for the bank as a whole:

- Are these the 2–3 questions you actually want answered before you roll a truck or pull the unit in?
- Which answers meaningfully change the **parts or time** you book? Those are the keepers.
- Is any question **useless without the unit in front of you**? Mark it to cut.
- Are the options the words an owner would use, and are they genuinely distinct?
- What's missing that you'd otherwise have to phone the customer for?

Mark up this file directly, or leave notes in issue #511. Anything that turns into a change
gets made against `RuleBasedCategorizationService.cs` with a link back to #511.

---

## Slides

*Slide-out rooms, mechanisms, motors, and seals.*

1. **Does the slide move at all when you operate the switch?**
   - No movement and no sound
   - Motor hums or clicks but nothing moves
   - Moves part way then stops
   - Moves, but slow, jerky, or noisy
   - *Help: The slide switch is usually inside near the door or on the main control panel.*
2. **Which slide is affected?**
   - Living room / main slide
   - Bedroom slide
   - Kitchen / galley slide
   - More than one slide
3. **Where is the slide stuck right now?**
   - Stuck fully out
   - Stuck fully in
   - Stuck part way out
   - Comes in but won't seal against the wall
4. **Have you noticed any of these at the slide?**
   - Grinding or binding noise
   - Visible damage to gears, rails, or arms
   - Water coming in past the seal
   - Nothing obvious
   - *Help: A photo of the mechanism from inside a storage bay or underneath helps the tech prepare.*

- [ ] Reviewed with a technician

## Electrical

*12V DC and 120V AC systems, batteries, wiring, inverters, converters, solar.*

1. **Is the problem with 12-volt power, 120-volt power, or both?**
   - 12-volt (interior lights, water pump, furnace fan)
   - 120-volt (wall outlets, microwave, air conditioner)
   - Both
   - Not sure
   - *Help: 12-volt runs off the RV batteries; 120-volt needs shore power or the generator.*
2. **When does the problem happen?**
   - Only on shore power
   - Only on battery
   - Only on the generator
   - All the time
3. **What are the house batteries doing?**
   - Won't hold a charge overnight
   - Reading low or dead
   - Seem fine
   - Not sure how to check
   - *Help: The panel by the door or a battery monitor usually shows charge level.*
4. **Have any breakers or fuses tripped?**
   - Yes, and resetting it didn't hold
   - Yes, resetting fixed it for now
   - No
   - Couldn't find the panel
   - *Help: There is a 120-volt breaker panel and a 12-volt fuse block, often near the door or under a seat.*

- [ ] Reviewed with a technician

## Plumbing & Water

*Fresh, grey, and black water systems, pump, water heater, and fixtures.*

1. **Where is the water showing up?**
   - Inside on the floor
   - In a storage bay or the underbelly
   - Outside under the RV
   - At one fixture (sink, toilet, shower)
2. **Is it fresh, gray, or black water?**
   - Fresh / clean supply water
   - Gray (sink and shower drains)
   - Black (toilet)
   - Not sure
   - *Help: Fresh is clean supply water; gray is from sinks and the shower; black is from the toilet.*
3. **When does the problem happen?**
   - Only with the water pump running
   - Only on city / campground water
   - Both
   - Even with the water off and hose disconnected
4. **Is the water heater involved?**
   - Yes — no hot water
   - Yes — leaking at the water heater
   - No
   - Not sure

- [ ] Reviewed with a technician

## HVAC

*Air conditioning, furnace, heat pump, thermostat, and ducting.*

1. **What part is not working?**
   - Air conditioner not cooling
   - Furnace / heat not working
   - Thermostat blank or unresponsive
   - Runs but short-cycles or freezes up
2. **Does the unit try to start?**
   - Fan runs but no cold or hot air
   - Nothing happens at all
   - Starts, then shuts off after a few minutes
   - Trips a breaker when it starts
3. **How many roof AC or furnace units are on the RV, and how many are affected?**
   - One unit — it's affected
   - Two units — one affected
   - Two units — both affected
   - Not sure
4. **When were the filters and return-air vents last cleaned?**
   - In the last month
   - A few months ago
   - Over a year ago, or never
   - Not sure
   - *Help: Dirty filters and blocked vents are the most common cause of weak airflow.*

- [ ] Reviewed with a technician

## Generator

*Onboard generator that will not start, runs rough, or produces no output.*

1. **What does the generator do when you start it?**
   - Cranks but won't fire
   - Clicks, or nothing at all
   - Starts, then shuts down
   - Runs, but no power at the outlets
   - *Help: Start it from the generator switch on the RV, not just the auto-start.*
2. **How much fuel is available to it?**
   - Main tank above a quarter
   - Main tank below a quarter
   - Runs off its own propane supply
   - Not sure
   - *Help: Most built-in generators stop drawing fuel once the main tank drops below about a quarter.*
3. **When did it last run normally?**
   - It has sat unused for months
   - Ran fine last trip
   - Has never run right since we got it
   - Not sure
4. **Any warning lights, error codes, or smells?**
   - Error code or blinking light on the generator
   - Smell of fuel or exhaust
   - Unusual noise or vibration
   - None of these

- [ ] Reviewed with a technician

## LP / Propane

*Propane tanks, regulators, lines, leaks, and detectors.*

1. **Do you smell propane right now?**
   - Yes — strong smell
   - Yes — faint or occasional
   - Only when an appliance runs
   - No
   - *Help: If the smell is strong, leave the RV, shut the tank valve, and don't use switches or flames until it's checked.*
2. **Which propane appliances are not working?**
   - Furnace
   - Water heater
   - Cooktop / oven
   - Refrigerator on propane
   - All of them
3. **Is the propane leak detector alarming?**
   - Yes — alarming now
   - It alarmed earlier, then stopped
   - No
   - Not sure which alarm that is
4. **Have the tanks just been filled or swapped?**
   - Yes — just filled or swapped
   - About half full
   - Near empty
   - Not sure
   - *Help: After a fill, turn the tank valve on slowly — opening it fast can lock out the regulator.*

- [ ] Reviewed with a technician

## Appliances & Refrigerator

*Refrigerator, cooktop, oven, microwave, washer / dryer, and ice maker.*

1. **Which appliance is the problem?**
   - Refrigerator
   - Cooktop / oven
   - Microwave
   - Washer / dryer
   - Ice maker
   - Other
2. **If it's the refrigerator, how is it powered when it fails?**
   - Fails on propane, works on electric
   - Fails on electric, works on propane
   - Fails on both
   - Not a refrigerator issue
   - *Help: Most RV refrigerators run on either propane or 120-volt electric.*
3. **Does the appliance show any sign of power?**
   - Lights or display on, but doesn't run
   - Completely dead
   - Works on and off
   - Not sure
4. **How cold is the refrigerator or freezer?**
   - Fridge warm, freezer still cold
   - Both warm
   - Cool, but not cold enough
   - Not a temperature problem

- [ ] Reviewed with a technician

## Roof & Seals

*Roof membrane, sealant, seams, window and slide seals, and water intrusion or leaks.*

1. **Where does the water come in?**
   - Around a roof vent or fan
   - Around the air conditioner
   - Along a slide-out
   - Around a window or marker light
   - Can't tell
2. **When do you notice the leak?**
   - During or after rain
   - After running the water system
   - Only with the slide in a certain position
   - All the time
   - *Help: A leak only in rain points to the roof or seals; a leak that tracks water use points to plumbing.*
3. **Is there a soft or spongy spot?**
   - Yes — on the roof
   - Yes — on an inside wall or ceiling
   - Yes — on the floor
   - No soft spots noticed
4. **When was the roof sealant last inspected or resealed?**
   - Within the last year
   - One to three years ago
   - Longer, or never
   - Not sure

- [ ] Reviewed with a technician

## Awning

*Awning fabric, arms, motors, and sensors.*

1. **What is the awning doing?**
   - Won't extend
   - Won't retract
   - Extends or retracts crooked or jerky
   - Fabric or an arm is damaged
   - *Help: Note whether it's a manual awning or powered from a switch.*
2. **Is it stuck open right now?**
   - Yes — stuck fully open
   - Yes — stuck part way
   - No — it's closed
   - Closed, but won't lock for travel
3. **Did anything happen right before it stopped working?**
   - Wind or a storm
   - It was hit, or something fell on it
   - Just stopped on its own
   - Not sure

- [ ] Reviewed with a technician

## Chassis & Running Gear

*Brakes, tires, wheel bearings, suspension, axles, and leveling or stabilizer jacks.*

1. **Which part of the running gear is the concern?**
   - Brakes
   - Tires or wheels
   - Wheel bearings or hubs
   - Suspension or axles
   - Leveling or stabilizer jacks
2. **Is the RV safe to move or tow right now?**
   - Yes — drives or tows normally
   - Drives, but something feels wrong
   - No — not safe to move
   - Not sure
   - *Help: A loud noise, smoke from a wheel, or a flat tire means treat it as not safe to move.*
3. **What do you notice while driving or towing?**
   - Grinding or squealing noise
   - Vibration or shaking
   - Pulling to one side
   - Smell of something hot
   - Nothing while moving
4. **If it's the leveling or stabilizer jacks, what happens?**
   - Won't extend
   - Won't retract
   - One corner won't move
   - Error on the leveling panel
   - Not a jack issue

- [ ] Reviewed with a technician

## Body & Exterior

*Exterior panels, fiberglass, trim, doors, windows, compartment latches, and hitch.*

1. **What part of the body or exterior is affected?**
   - An entry or compartment door
   - A window
   - A latch, lock, or hinge
   - Exterior panel, trim, or fiberglass
   - Steps or entry assist
2. **Does it still work, or is it stuck?**
   - Opens and closes, but won't seal or latch
   - Stuck open
   - Stuck closed
   - Works, but damaged
   - *Help: A compartment door that won't latch matters before travel — say which one.*
3. **How did it start?**
   - Wind caught it, or it happened in transit
   - Impact or something struck it
   - Just wore out or stopped working
   - Was like this when we got the RV
4. **Is weather getting in?**
   - Yes — water or a draft is coming in
   - Not yet, but it won't seal
   - No
   - Not sure

- [ ] Reviewed with a technician

## Interior & Cabinetry

*Cabinets, furniture, flooring, blinds, and interior trim.*

1. **What inside the RV needs attention?**
   - A cabinet or drawer
   - A door or latch
   - Furniture (sofa, dinette, bed)
   - Flooring
   - Blinds or window coverings
   - Trim or molding
2. **What is wrong with it?**
   - Broken or cracked
   - Loose or pulling away from the wall or floor
   - Won't open, close, or latch
   - Water-stained or swollen
   - *Help: Swollen or stained cabinetry can be a sign of a leak — say where.*
3. **Did it happen in transit?**
   - Yes — after a trip
   - Gradually over time
   - Was like this at delivery
   - Not sure

- [ ] Reviewed with a technician

## Other

*Anything not covered by the categories above.*

1. **In your own words, what is happening?**
   - *(free text only)*
   - *Help: Include when it started, how often it happens, and anything you've noticed — sounds, smells, leaks, lights.*
2. **When did the problem start?**
   - Today
   - This week
   - This month
   - More than a month ago
3. **How would you describe it now?**
   - Constant
   - Comes and goes
   - Getting worse
   - Only in certain conditions
4. **Is the RV usable right now?**
   - Yes — fully usable
   - Usable with workarounds
   - Not usable
   - Not safe to use
   - *Help: This helps us judge how quickly you need to be seen.*

- [ ] Reviewed with a technician

---

## Fallback of the fallback

If a category string arrives that is not in the vocabulary at all (should not happen — every
vocabulary code above has its own set), the service returns a generic three-question set:
*"Can you describe the issue in more detail?"* / *"When did you first notice the problem?"* /
*"Is the issue intermittent or constant?"*
