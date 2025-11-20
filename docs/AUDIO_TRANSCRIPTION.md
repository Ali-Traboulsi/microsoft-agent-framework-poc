# Audio Transcription Support

## Overview

The multi-modal API now supports **audio files** through automatic transcription using OpenAI's Whisper API. When you upload an audio file, it's automatically transcribed to text and sent to the chat model.

## How It Works

```
User uploads audio.mp3
    ↓
AudioTranscriptionService detects audio file
    ↓
Calls OpenAI Whisper API → transcribes to text
    ↓
Sends transcript to gpt-4o with user's message
    ↓
Returns AI analysis of the transcript
```

## Supported Audio Formats

- **MP3** (.mp3) - MPEG audio
- **WAV** (.wav) - Waveform audio
- **M4A** (.m4a) - MPEG-4 audio
- **WebM** (.webm) - WebM audio
- **OGG** (.ogg) - Ogg Vorbis
- **FLAC** (.flac) - Free Lossless Audio Codec

## API Endpoints

### Form-Data Upload (Recommended)

**Endpoint:** `POST /api/v2/MasterAgent/chat/multimodal/upload`

**Content-Type:** `multipart/form-data`

**Form Fields:**
- `message` (text): Your question about the audio
- `files` (file): The audio file(s) to transcribe
- `conversationId` (text, optional): Conversation tracking ID

**Example Postman Request:**

| Key | Type | Value |
|-----|------|-------|
| `message` | Text | `Summarize what the speaker says` |
| `files` | File | `meeting-recording.mp3` |

**Example Response:**
```json
{
    "success": true,
    "data": {
        "message": "[Audio transcription from meeting-recording.mp3]: Today we discussed quarterly results and next quarter's goals...\n\nThe speaker discussed quarterly results, highlighting a 15% increase in revenue...",
        "timestamp": "2025-11-20T12:00:00Z",
        "processingTimeMs": 5234,
        "contentTypesProcessed": ["audio"],
        "conversationId": "uuid-here"
    }
}
```

### JSON with Base64 (Alternative)

**Note:** Audio transcription is **only supported via form-data upload**. The JSON endpoint (`/chat/multimodal`) doesn't support audio files because:
1. Base64-encoded audio files are very large
2. OpenAI chat models don't accept audio in messages
3. Audio must be transcribed separately via Whisper API

## Configuration

### Required Environment Variables

```bash
export OPENAI_API_KEY="sk-..."
```

The same API key is used for both:
- **Whisper API** (audio transcription)
- **GPT-4o API** (chat responses)

### Required Model Access

Your OpenAI account must have access to:
- `whisper-1` - For audio transcription
- `gpt-4o` - For chat responses with transcripts

## Code Examples

### Backend (C#)

#### AudioTranscriptionService

```csharp
public class AudioTranscriptionService
{
    public async Task<string> TranscribeAudioAsync(
        byte[] audioData,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Calls OpenAI Whisper API
        // Returns transcript as plain text
    }

    public static bool IsAudioFile(string mediaType)
    {
        return mediaType.StartsWith("audio/");
    }
}
```

#### ContentConverter (Automatic Detection)

```csharp
// In ConvertFormFilesToAIContents:
var mediaType = DetectMediaType(file.FileName);

if (AudioTranscriptionService.IsAudioFile(mediaType))
{
    // Transcribe audio to text
    var transcript = await audioService.TranscribeAudioAsync(
        fileBytes,
        file.FileName
    );
    
    contents.Add(new TextContent(
        $"[Audio transcription from {file.FileName}]: {transcript}"
    ));
}
else
{
    // Handle images/PDFs as base64
    var dataUri = $"data:{mediaType};base64,{base64Data}";
    contents.Add(new DataContent(dataUri, mediaType));
}
```

### Frontend (React/TypeScript)

```typescript
// Upload audio file
const formData = new FormData();
formData.append('message', 'Transcribe and summarize this');
formData.append('files', audioFile); // File object from input

const response = await fetch(
  'http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload',
  {
    method: 'POST',
    body: formData,
  }
);

const result = await response.json();
console.log(result.data.message); // Contains transcript + AI analysis
```

## Error Handling

### Common Errors

**1. "Audio transcription service is required for audio files"**
- Cause: AudioTranscriptionService not injected in controller
- Fix: Ensure `AddHttpClient<AudioTranscriptionService>()` is in Program.cs

**2. HTTP 400 - "invalid_request_error: invalid_value"**
- Cause: Trying to send audio base64 to chat model (not supported)
- Fix: Use form-data upload endpoint instead of JSON endpoint

**3. "Failed to transcribe audio: Unauthorized"**
- Cause: Invalid or missing OPENAI_API_KEY
- Fix: Set valid API key in environment or appsettings.json

**4. "Failed to transcribe audio: File too large"**
- Cause: Audio file exceeds Whisper API limit (25MB)
- Fix: Compress or split audio file before upload

## Limitations

1. **Max file size:** 25MB per audio file (Whisper API limit)
2. **Language support:** Whisper auto-detects language, but works best with English
3. **Streaming:** Audio transcription adds latency (typically 2-5 seconds)
4. **Cost:** Whisper API charges $0.006 per minute of audio

## Best Practices

### 1. Provide Context in Message

**Good:**
```json
{
  "message": "This is a customer support call. Identify the main issue and resolution."
}
```

**Bad:**
```json
{
  "message": "Analyze this"
}
```

### 2. Optimize Audio Quality

- Use clear audio recordings (minimal background noise)
- Avoid overlapping speakers
- Prefer mono over stereo (smaller file size)

### 3. Handle Long Audio Files

For audio >5 minutes:
1. Consider splitting into segments
2. Use batch processing
3. Provide timestamps in your message

### 4. Combine with Other Content

You can upload audio + images together:

| Key | Type | Value |
|-----|------|-------|
| `message` | Text | `Compare the audio description with this diagram` |
| `files` | File | `narration.mp3` |
| `files` | File | `diagram.png` |

## Testing

### Quick Test with cURL

```bash
curl -X POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload \
  -F "message=Transcribe this audio" \
  -F "files=@recording.mp3"
```

### Postman Collection

Import the test collection: `docs/MultiModal-Tests.postman_collection.json`

It includes:
- Audio file upload example
- Multiple audio files
- Audio + image combination

## Architecture

### Dependency Injection

```csharp
// Program.cs
builder.Services.AddHttpClient<AudioTranscriptionService>();
```

### Flow Diagram

```
MasterAgentController
    ↓ (inject)
AudioTranscriptionService
    ↓ (uses)
HttpClient → OpenAI Whisper API
    ↓ (returns)
Transcript (string)
    ↓ (wrapped in)
TextContent → List<AIContent>
    ↓ (sent to)
MasterOrchestrator → gpt-4o
```

## Related Documentation

- [Multi-Modal Usage Guide](MULTIMODAL_USAGE.md) - General multi-modal API usage
- [Postman Testing Guide](POSTMAN_MULTIMODAL_TESTING.md) - Testing with Postman
- [Quick Reference](MULTIMODAL_QUICK_REFERENCE.md) - Quick lookup

## Troubleshooting

**Q: Can I use SignalR streaming with audio files?**

A: No, SignalR doesn't support file uploads. For audio transcription, use the HTTP endpoint `/chat/multimodal/upload`.

**Q: Why is audio transcription slower than image analysis?**

A: Audio transcription requires two API calls:
1. Whisper API (2-4 seconds)
2. GPT-4o API (1-2 seconds)

Total: ~3-6 seconds vs 1-2 seconds for images

**Q: Can I get the raw transcript without AI analysis?**

A: Yes, check the response message - it includes the raw transcript prefixed with `[Audio transcription from filename]:` before the AI's analysis.

**Q: What if my audio is in a different language?**

A: Whisper auto-detects 99+ languages. You can optionally specify language in the API request (future enhancement).
