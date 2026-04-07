# CollaborationEngine.API (Demo)

This project is a lightweight **demo Collaboration Engine** built on **ASP.NET Core + SignalR + WebRTC**.

- SignalR is used for:
  - chat messages
  - typing indicator
  - session events (join/request agent/supervisor)
  - WebRTC signaling (offer/answer/ICE)
- WebRTC is used for:
  - live screen-share streaming (User -> Agent/Supervisor)
- Recording is saved by:
  - browser **MediaRecorder** recording the shared screen stream
  - then uploading the `.webm` file to the API

> Notes
> - This demo uses **local JSON storage** under `Data/` (no database).
> - This demo uses a single `wwwroot/index.html` UI for User/Agent/Supervisor.

---

## 1) Run the API

From the `CollaborationEngine.API` folder:

```powershell
dotnet run
```

The console will print the API URL(s), typically:

- `https://localhost:7081`

Open the UI:

- `https://localhost:7081/`

---

## 2) Demo flow (multi-window)

### User window

1. Select **User** tab
2. Login
3. Click **New Session** (or it may auto-create)
4. Click **Request Human** (assigns an agent)
5. Use chat
6. Click **Share Screen**
   - starts screen capture (`getDisplayMedia`)
   - starts WebRTC signaling via SignalR
   - starts local browser recording automatically

### Agent window

1. Open a second window/tab on the same URL
2. Select **Agent** tab
3. Login with one of the seeded agent emails
4. The agent is auto-joined to assigned sessions
5. Agent can:
   - chat
   - view live stream

### Supervisor window

1. Open a third window/tab
2. Select **Supervisor** tab
3. Login with seeded supervisor email
4. Supervisor can:
   - view active sessions
   - view recordings gallery

---

## 3) Seeded demo accounts

Seed data is stored under:

- `Data/agents.json`
- `Data/supervisors.json`

The demo is configured to only allow known agents/supervisors from these files.

---

## 4) WebRTC architecture (how streaming works)

### Live stream path

- Media transport: **WebRTC peer-to-peer** (browser-to-browser)
- Signaling: **SignalR Hub** (`/collaborationHub`)

### Signaling messages via SignalR

These are exchanged via the hub to establish the WebRTC connection:

- Offer (SDP)
- Answer (SDP)
- ICE candidates

### Where it is implemented

- UI/Client: `wwwroot/index.html`
- Hub methods: `Hubs/CollaborationHub.cs`

---

## 5) TURN / STUN settings

The ICE servers are configured in:

- `wwwroot/index.html` → `ensurePeerConnection()` → `iceServers`

### Current configuration

- **STUN**
  - `stun:57.151.97.238:3478`

- **TURN**
  - `turn:57.151.97.238:3478?transport=udp`
  - `turn:57.151.97.238:3478?transport=tcp`
  - `username: readi`
  - `credential: NASS9411!!!`

### Force TURN (relay) for testing

Add this querystring to the page URL:

- `?forceRelay=1`

This sets `iceTransportPolicy = 'relay'`, which forces WebRTC to use TURN (relay). This is useful to validate TURN connectivity.

To verify, open:

- `chrome://webrtc-internals`

Look for:

- candidate type `relay`

---

## 6) Recording architecture (how recording is saved)

### What happens

1. When the User starts screen sharing, the browser starts a `MediaRecorder(localStream)`.
2. When sharing stops, `MediaRecorder` produces a `.webm` blob.
3. The UI uploads the blob via HTTP multipart upload.

### Upload API

- `POST /api/recording/session/{collaborationId}/recording/upload`

### Where files are stored

On the API server filesystem:

- `wwwroot/recordings/{collaborationId}/...webm`

### Recording controller

- `Controllers/RecordingController.cs`

---

## 7) Troubleshooting

### Live stream shows black / requires clicking play

The UI is configured to autoplay and calls `video.play()` on track receipt.

If audio is required, browsers may still require a user gesture before unmuting.

### `connectionState=failed` in `chrome://webrtc-internals`

When forcing TURN (`?forceRelay=1`), failures usually mean TURN is not reachable.

Common issues:

- port `3478` blocked (UDP/TCP)
- TURN relay port range blocked (coturn needs a high port range open)
- invalid TURN username/password

---
