## Architecture
- must be mobile first for users, desktop version for service managers 
- must support multiple dealers with single (optional) customer profile
- must support private labelling per dealer
- must support QR code system for easy dealer/customer connectivity 
- must support dealer specific deep linking for submission 
- should support dealer search if no link/QR code available


## Thoughts
- vin photo convert to structured vin
- vin lookup to popuplate info
- photos
- possible to lookup factory/extended warranty plan info?  
- Optional customer profile (prompt to create after submission) but otherwise no account required to reduce friction
- contact info from customer profile (if available)
- full time 
- urgency

- sending texts, emails, in-app updates
- what about sending above as a text? 

## Features
Best wedge features

- VIN scanner from phone camera
- photo/video diagnostics
- AI-assisted issue classification with technician-ready summaries
- AI-assisted problem description
- AI-guided issue wizard:  ie if category = fridge, ask type of fridge, error codes?, connected to shore power? 
- M: speech to text for initial problem description, then AI assisted cleanup and customer confirmation / customer communication
- speech to text for technical entry, then convert to structured
- service capacity forecasting:  based on failing parts captured across the dataset, predict/make maint recommendations? 


## Basic components
### Customer Service Request Form

**Fields:**

VIN
Make/model
Description
Photos/videos
Contact info

### Dealer Request Queue

A dashboard showing:

• new requests
• issue category
• customer info
• attachments

## Questions
- how to integrate with DMS (export to csv?  what format?  export PDF to upload to DMS as backup?)
- how to route submissions to technicians? 
- how to discovery/submit to the correct participating dealer? 
- how to position for later acuisition? 

## Focus area
- customer experience (existing DMS has poor customer interface) 
    - profile with contact info, VIN, etc for easy repeat tickets and submit to different dealers
    - AI:  photo to structure conversion (VIN, documents, etc)
    - customer status view 
- tech experience
    - minimize entry friction/time wherever possible



