# Project Overview

This project demonstrates advanced messaging patterns using MassTransit. It simulates a distributed system with publishers, controllers, and subscribers, including error handling, statistics, and encrypted communication.

## Processes

### Publisher (W) – 10%

- Publishes a `Publ` message every second with incrementing numbers (1, 2, 3, ...).
- Responds to commands from the controller.

### Generator Controller (K) – 15%

- Pressing **"s"** sends `Set { active = true }` (start).
- Pressing **"t"** sends `Set { active = false }` (stop).

### Subscribers A and B – 20%

- Both receive messages from the Publisher and print their content to the console.
- **Subscriber A** responds to messages divisible by 2: `RespA { who = "subscriber A" }`.
- **Subscriber B** responds to messages divisible by 3: `RespB { who = "subscriber B" }`.
- The Publisher prints the content of the responses to the console.

### Exceptions – 25%

- The Publisher, when handling responses, throws an exception with a probability of 1/3.
- Subscribers notify when their response caused an exception.
- The Publisher retries message handling up to 5 times.

### Message Observers – 20%

- Pressing **"s"** (in Publisher) prints statistics:
    - Number of message handling attempts (per type).
    - Number of successfully handled messages (per type).
    - Number of published messages (per type).

### Encryption – 10%

- Communication between the Controller and Publisher is encrypted with a shared key (student index number repeated).
- Each message uses a unique initialization vector.

---
