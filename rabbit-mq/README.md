## Project Tasks Overview

This project demonstrates various messaging patterns using MassTransit and RabbitMQ. Below are the tasks and their requirements:

### 1. Sender – 20%
- Sends 10 messages with varying content.

### 2. Receiver – 15%
- Prints the content of messages received from the sender to the console.

### 3. Headers – 10%
- The sender sets two different headers in each message.
- The receiver prints both headers for each received message.

### 4. Second Receiver – 10%
- Competing consumers: the sender sends 10 messages, the first receiver processes e.g., 4 messages, the second receiver processes 6 messages.

### 5. Acknowledgements – 15%
- Receivers acknowledge each message after a 2-second delay.
- Receivers should not receive the next message until the previous one is acknowledged.
- Run the sender first (it sends all messages), then start one receiver (it receives one message), after a few seconds start the second receiver (it should receive the next message from the queue).

### 6. Responses – 10%
- The second receiver replies to messages.
- The sender prints the content of the responses to the console.

### 7. Publish/Subscribe – 20%
- 1 publisher, 2 subscribers.
- The publisher sends 10 messages alternately to channels `abc.def` and `abc.xyz`.
- The first subscriber receives messages from channels starting with `abc`.
- The second subscriber receives messages from channels ending with `xyz`.
- Messages from the `abc.xyz` channel should be delivered to both subscribers.

---

Follow these tasks to implement and demonstrate messaging patterns with MassTransit and RabbitMQ.