## Project Tasks Overview

This project demonstrates various messaging patterns using MassTransit. The tasks are divided as follows:

### 1. Publisher (W) – 20%
- Publishes 10 messages with dynamically changing content.

### 2. Consumer (A) – 15%
- Displays the content of received messages in the console.

### 3. Headers – 10%
- The sender sets 2 different headers in each message.
- The receiver displays these headers.

### 4. Consumer (B) – 15%
- Displays the content of received messages and a counter of received messages.
- The counter must be a non-static field of the class.

### 5. Consumer (C) and Second Message Type – 20%
- The publisher sends messages of a second type (using an interface).
- Consumer C displays these messages in the console.

### 6. Versioning – 20%
- The publisher sends 3 types of messages.
- Type 3 inherits from types 1 and 2.
- Consumer B receives all three types.

---
