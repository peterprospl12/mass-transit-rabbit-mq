# MassTransit Sagas: Order Processing

This project demonstrates a distributed order processing system using MassTransit Sagas. The system consists of several processes (services) that communicate via messages to handle customer orders, confirmations, and inventory management.

## Processes

- **Store**
- **Client A**
- **Client B**
- **Warehouse**

## Messages

Each message can include additional fields as needed.

| Message                        | Direction                | Description                                   |
|---------------------------------|--------------------------|-----------------------------------------------|
| `StartOrder { int quantity; }`  | Client → Store           | Client initiates an order                     |
| `RequestConfirmation { int quantity; }` | Store → Client    | Store asks client to confirm the order        |
| `Confirmation`                  | Client → Store           | Client confirms the order                     |
| `NoConfirmation`                | Client → Store           | Client declines the order                     |
| `RequestAvailability { int quantity; }` | Store → Warehouse | Store asks warehouse for item availability    |
| `AvailabilityResponse`          | Warehouse → Store        | Warehouse confirms availability               |
| `NegativeAvailabilityResponse`  | Warehouse → Store        | Warehouse denies availability                 |
| `OrderAccepted { int quantity; }` | Store → Client         | Store confirms order acceptance               |
| `OrderRejected { int quantity; }` | Store → Client         | Store rejects the order                       |

---

## Process Details

### Store (40% + 10% for timeout handling)

- Manages and processes sagas describing the order process.
- Sends availability requests to the warehouse (with quantity).
- Sends confirmation requests to the client (with quantity).
- Receives confirmations from both warehouse and client.
- The order of receiving confirmations does not matter.
- If both parties confirm, the transaction succeeds and a confirmation is sent to the client.
- Otherwise, a rejection is sent to the client.
- **Timeout:** If the order is not confirmed within 10 seconds, it is automatically cancelled.

### Clients (Client A & Client B) (20%)

- Send orders specifying the quantity (entered via keyboard).
- Receive confirmation requests from the store and respond (confirmation or rejection, both signaled by messages) based on user input.
- Receive final confirmation or rejection messages from the store.

### Warehouse (30%)

- Maintains warehouse state (integer value, initially 0), divided into "available" and "reserved" units.
- Displays the current warehouse state (available and reserved units separately) on the console.
- Entering a number increases the available units by the given value.
- Receives availability requests from the store and responds (confirmation or rejection, both signaled by messages).
- Responds positively if the number of available units is at least equal to the requested quantity.
- Otherwise, responds negatively.
- Confirming a request reserves the specified quantity (moves units from available to reserved).
- Accepting an order removes reserved units.
- Rejecting an order moves reserved units back to available.

---

## Process Flow Diagram

```text
+-----------+      Confirmation? YES/NO      +-----------+
| Warehouse |------------------------------->|   Store   |
+-----------+                                +-----------+
      ^                                            ^
      |                                            |
      |                                            |
      |                                            |
+-----------+                                +-----------+
|  Client1  |-------------------\            |   Store   |
| (Sends    |                    \           | (Receives |
|  Order)   |                     ---------->|  Orders,  |
+-----------+                    /           |  Sends    |
                                /            |  Requests)|
+-----------+                  /             +-----------+
|  Client2  |-------------------             /    |    \
| (Sends    |                               /     |     \ Confirmation? YES/NO
|  Order)   |                              /      |      \
+-----------+                             /       |       \
      |                                  /        |        \
      | Confirmation? YES/NO            /         |         \
      |<-------------------------------<          |          >-----------------------------> To Client1
      |                                           |
      |<------------------------------------------+----------------------------------------> To Client2
      |
      |
      +--------------------------------------------------------------------------------------|
      |                                                                                      |
      | If 2 Confirmations (Warehouse + Client)                                              |
      +--------------------------------------> SUCCESS (Store sends OrderAccepted to Client)  |
      |                                                                                      |
      | If 1 or 0 Confirmations                                                              |
      +--------------------------------------> FAILURE (Store sends OrderRejected to Client)  |
      +--------------------------------------------------------------------------------------+

```

---

## Summary

This example demonstrates a distributed saga pattern for order processing, including:

- Asynchronous communication between services
- Timeout and compensation logic
- State management in a distributed environment

