# Collaboration Engine — API & Realtime (SignalR) Documentation

**Project:** `CollaborationEngine.API`

This document is **Word-friendly** Markdown. You can:

1. Open this file in VS Code / Visual Studio.
2. Copy all content.
3. Paste into Microsoft Word.
4. Save as `.docx`.

---

## 1) Base URLs

### Local (typical)

- **UI:** `https://localhost:7081/`
- **REST API base:** `https://localhost:7081/api`
- **SignalR Hub:** `https://localhost:7081/collaborationHub`

> Your ports may differ; check the console output from `dotnet run`.

---

## 2) Roles / Pages

The demo UI is served from:

- `GET /` → `wwwroot/index.html`

The UI supports 3 roles:

- **User**: creates collaboration sessions, chats, shares screen.
- **Agent**: receives assignment to sessions, chats, watches stream.
- **Supervisor**: views active sessions and recordings gallery.

---

## 3) Data Storage (Demo)

This demo uses **local JSON files** under the project folder:

- `Data/collaborationSessions.json`
- `Data/chatMessages.json` (if present)
- `Data/agents.json`
- `Data/supervisors.json`
- `Data/users.json`

Recordings are stored on disk under:

- `wwwroot/recordings/<collaborationId>/*.webm`

---

# 4) REST API Endpoints

All endpoints are implemented using ASP.NET Core controllers under `Controllers/`.

---

## 4.1 AuthController

**Controller:** `Controllers/AuthController.cs`

**Base route:** `/api/auth`

### POST `/api/auth/login/user`

**Purpose:** Demo login for a user.

**Request body** (`application/json`):

```json
{
  "email": "user@example.com",
  "apiKey": "(optional demo value)"
}
```

**Response** (`200 OK`):

```json
{
  "token": "...",
  "userType": "User",
  "user": { "id": 1, "name": "...", "email": "..." }
}
```

### POST `/api/auth/login/agent`

**Purpose:** Login as an Agent.

**Important:** Agent email must exist in `Data/agents.json`.

**Request body**:

```json
{
  "email": "ashok@company.com",
  "apiKey": "(ignored for demo)"
}
```

**Response** (`200 OK`):

```json
{
  "token": "...",
  "userType": "Agent",
  "agent": { "id": 1, "name": "Ashok", "email": "...", "isAvailable": true }
}
```

### POST `/api/auth/login/supervisor`

**Purpose:** Login as a Supervisor.

**Important:** Supervisor email must exist in `Data/supervisors.json`.

**Request body**:

```json
{
  "email": "rajiv@company.com",
  "apiKey": "(ignored for demo)"
}
```

**Response** (`200 OK`):

```json
{
  "token": "...",
  "userType": "Supervisor",
  "supervisor": { "id": 1, "name": "Rajiv", "email": "..." }
}
```

### POST `/api/auth/validate`

**Purpose:** Validate a demo token.

**Request body** (`application/json`):

```json
"demo-token-string"
```

**Response** (`200 OK`):

```json
{ "userId": "1", "userType": "User" }
```

---

## 4.2 CollaborationController

**Controller:** `Controllers/CollaborationController.cs`

**Base route:** `/api/collaboration`

### POST `/api/collaboration/session/create`

**Purpose:** Create a new collaboration session.

**Request body**:

```json
{ "userId": 1, "applicationId": 1 }
```

**Response** (`200 OK`):

```json
{
  "collaborationId": "df58686054a048bf",
  "sessionId": 2,
  "startedAt": "2026-04-06T01:10:45.3386419Z"
}
```

### GET `/api/collaboration/session/{collaborationId}`

**Purpose:** Fetch session info.

**Response** (`200 OK`):

```json
{
  "id": 2,
  "collaborationId": "df58686054a048bf",
  "status": "Active",
  "startedAt": "...",
  "endedAt": null,
  "userId": 1,
  "agentId": 2,
  "agentName": "Ashok",
  "userName": "..."
}
```

### GET `/api/collaboration/sessions/active`

**Purpose:** List active sessions (used by Supervisor UI).

**Response**:

```json
[
  {
    "id": 2,
    "collaborationId": "df58686054a048bf",
    "startedAt": "...",
    "userId": 1,
    "agentId": 2
  }
]
```

### POST `/api/collaboration/session/{collaborationId}/end`

**Purpose:** End a session.

**Response**:

```json
{ "message": "Session ended successfully" }
```

### GET `/api/collaboration/session/{collaborationId}/messages`

**Purpose:** Fetch chat history for the session.

**Response**:

```json
[
  {
    "id": 10,
    "content": "Hello",
    "messageType": "Text",
    "sentAt": "...",
    "senderType": "User",
    "senderId": 1
  }
]
```

### POST `/api/collaboration/session/{collaborationId}/message`

**Purpose:** Add a message via REST (demo mode; generally UI uses SignalR `SendMessage`).

**Request body**:

```json
{ "content": "Hello" }
```

**Response**:

```json
{
  "id": 11,
  "content": "Hello",
  "messageType": "Text",
  "sentAt": "...",
  "senderType": "User",
  "senderId": 1
}
```

### POST `/api/collaboration/session/{collaborationId}/recording/start`

**Purpose:** Mark session as recording (server state) and return a recording URL.

**Response**:

```json
{ "recordingUrl": "..." }
```

### POST `/api/collaboration/session/{collaborationId}/recording/stop`

**Purpose:** Mark session recording stopped.

**Response**:

```json
{ "recordingUrl": "..." }
```

### GET `/api/collaboration/session/{collaborationId}/recording/status`

**Purpose:** Get current recording status.

**Response**:

```json
{ "isRecording": false, "recordingUrl": "..." }
```

### GET `/api/collaboration/session/{collaborationId}/stream/url`

**Purpose:** Generate a stream URL (legacy / informational). Live streaming is handled via WebRTC.

**Response**:

```json
{ "streamUrl": "wss://localhost:7080/stream/<collaborationId>" }
```

---

## 4.3 RecordingController

**Controller:** `Controllers/RecordingController.cs`

**Base route:** `/api/recording`

### POST `/api/recording/session/{collaborationId}/recording/upload`

**Purpose:** Upload a `.webm` recording file to the server.

**Request:** `multipart/form-data`

- form field: `file` → the `.webm` blob

**Response**:

```json
{
  "recordingUrl": "https://localhost:7081/recordings/<collaborationId>/<fileName>",
  "fileName": "<collaborationId>_20260406_011045.webm",
  "size": 123456,
  "contentType": "video/webm"
}
```

### GET `/api/recording/session/{collaborationId}/recordings`

**Purpose:** List recordings for one session.

**Response**:

```json
[
  {
    "fileName": "...webm",
    "collaborationId": "...",
    "size": 123,
    "created": "...",
    "url": "https://localhost:7081/recordings/.../....webm"
  }
]
```

### GET `/api/recording/session/all/recordings`

**Purpose:** List recordings across all sessions.

### GET `/api/recording/recordings/{collaborationId}/{fileName}`

**Purpose:** Download a recording via the controller.

> Recordings are also served via static files from `/recordings/...`.

---

## 4.4 ManagementController

**Controller:** `Controllers/ManagementController.cs`

**Base route:** `/api/management`

### POST `/api/management/application/register`

Registers an application.

### POST `/api/management/user/register`

Registers a user.

### POST `/api/management/user/register/simple`

Registers a user (idempotent / returns existing).

### POST `/api/management/agent/register`

Registers an agent.

### POST `/api/management/supervisor/register`

Registers a supervisor.

### GET `/api/management/agents/available`

Lists available agents.

### GET `/api/management/supervisors/available?currentAgentId={id}`

Lists available supervisors.

### POST `/api/management/agent/{agentId}/availability`

Set agent availability.

### POST `/api/management/supervisor/{supervisorId}/availability`

Set supervisor availability.

---

# 5) SignalR Hub (Realtime) — CollaborationHub

**Hub URL:** `/collaborationHub`

The UI connects with query string identity:

- `?userType=user|agent|supervisor`
- `&userId=<id>`

Example:

```
https://localhost:7081/collaborationHub?userType=agent&userId=2
```

---

## 5.1 Hub Methods (Client -> Server)

These are invoked from the browser via `connection.invoke(...)`.

### `JoinCollaboration(collaborationId)`

- Adds the connection to a SignalR group named by `collaborationId`.
- Sends chat history to the caller.

### `LeaveCollaboration(collaborationId)`

- Removes the connection from the group.

### `SendMessage(collaborationId, message)`

- Persists chat message to local JSON
- Broadcasts to group via `ReceiveMessage`
- If sender is User and no agent assigned, may auto-assign an agent.

### `RequestAgent(collaborationId)`

- Assigns an available agent and broadcasts `AgentAssigned`.

### `RequestSupervisor(collaborationId)`

- Agent-only: requests a supervisor list. Returns `AvailableSupervisors` to caller.

### `AddSupervisor(collaborationId, supervisorId)`

- Agent-only: assigns the selected supervisor and broadcasts `SupervisorAdded`.

### `StartScreenShare(collaborationId)`

- Broadcasts `ScreenShareStarted` to group.
- Live stream media itself is WebRTC (not SignalR).

### `StopScreenShare(collaborationId)`

- Broadcasts `ScreenShareStopped`.

### WebRTC signaling

- `SendWebRTCOffer(collaborationId, offer)` → broadcasts `ReceiveWebRTCOffer`
- `SendWebRTCAnswer(collaborationId, answer)` → broadcasts `ReceiveWebRTCAnswer`
- `SendWebRTCIceCandidate(collaborationId, candidate)` → broadcasts `ReceiveWebRTCIceCandidate`

### Typing indicator

- `SetTyping(collaborationId, isTyping)` → broadcasts `Typing`

### Recording state events

- `StartRecording(collaborationId)` → broadcasts `RecordingStarted`
- `StopRecording(collaborationId)` → broadcasts `RecordingStopped`

### Agent convenience

- `GetMyAssignedSession()`
  - Agent-only helper to find currently assigned active session.

---

## 5.2 Hub Events (Server -> Client)

These are received in the browser via `connection.on(...)`.

- `AssignedToSession`
- `UserJoined`
- `UserLeft`
- `ChatHistory`
- `ReceiveMessage`
- `AgentAssigned`
- `AvailableSupervisors`
- `SupervisorAdded`
- `ScreenShareStarted`
- `ScreenShareStopped`
- `ReceiveWebRTCOffer`
- `ReceiveWebRTCAnswer`
- `ReceiveWebRTCIceCandidate`
- `Typing`
- `RecordingStarted`
- `RecordingStopped`
- `Error`

---

# 6) WebRTC + TURN/STUN (Client side)

The WebRTC peer connection is created in:

- `wwwroot/index.html` → `ensurePeerConnection()`

### Current ICE servers

- STUN: `stun:57.151.97.238:3478`
- TURN:
  - `turn:57.151.97.238:3478?transport=udp`
  - `turn:57.151.97.238:3478?transport=tcp`
  - username: `readi`
  - credential: `NASS9411!!!`

### Force TURN relay test

Add to URL:

- `?forceRelay=1`

Then confirm relay candidates in:

- `chrome://webrtc-internals`

---

# 7) Recording (Client + Server)

### What records the video

- Browser `MediaRecorder(localStream)` records the screen-share stream.

### How it is saved

- When recording stops, UI uploads `.webm` to:
  - `POST /api/recording/session/{collaborationId}/recording/upload`

### Where it is stored

- `wwwroot/recordings/{collaborationId}/...webm`

---

# 8) Suggested production improvements (optional)

For real cloud hosting, consider:

- Move JSON persistence to a DB (SQL/Postgres/Cosmos)
- Store recordings in object storage (Azure Blob / S3)
- Use short-lived TURN credentials (do not hardcode in frontend)
- Add TURN/TLS (`turns:` / 5349) for strict networks
- Consider SFU (LiveKit/Janus/mediasoup) for multi-viewer scaling + server-side recording
