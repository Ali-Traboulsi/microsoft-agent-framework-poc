# Multi-Modal Input Usage Guide

## Overview

The Master Agent now supports multi-modal input, allowing you to send text, images, audio files, and documents in a single request. This is powered by Microsoft Agent Framework's native `ChatMessage` with `AIContent` types.

## Supported Content Types

| Type | Description | Input Format | Example Use Case |
|------|-------------|--------------|------------------|
| `text` | Plain text messages | `text` property | "Analyze this portfolio" |
| `image` | Images (PNG, JPEG, GIF, WebP) | Base64 data URI or URL | "What's in this chart?" |
| `audio` | Audio files (MP3, WAV, OGG) | Base64 data URI or URL | "Transcribe this call" |
| `file` | Documents (PDF, etc.) | Base64 data URI | "Summarize this report" |
| `uri` | External resource URLs | URL string | "Analyze https://..." |

## API Endpoint

**POST** `/api/v2/MasterAgent/chat/multimodal`

### Request Format

```json
{
  "message": "Optional text message",
  "contents": [
    {
      "type": "text",
      "text": "What do you see in this image?"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgo...",
      "mediaType": "image/png"
    }
  ],
  "conversationId": "optional-conversation-id"
}
```

### Response Format

```json
{
  "success": true,
  "data": {
    "message": "I can see a portfolio performance chart showing...",
    "timestamp": "2024-01-15T10:30:00Z",
    "processingTimeMs": 1250,
    "contentTypesProcessed": ["text", "image"],
    "conversationId": "conv-123"
  },
  "error": null
}
```

## Examples

### 1. Image Analysis

**Request:**
```json
{
  "message": "Analyze this stock chart and provide investment recommendations",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgoAAAANS...",
      "mediaType": "image/png"
    }
  ]
}
```

**Use Case:** Upload a stock chart or portfolio visualization and get AI-powered analysis.

### 2. Audio + Text Request

**Request:**
```json
{
  "message": "Transcribe this client call and extract investment preferences",
  "contents": [
    {
      "type": "audio",
      "data": "data:audio/wav;base64,UklGRiQAAABXQVZF...",
      "mediaType": "audio/wav",
      "fileName": "client_call.wav"
    }
  ]
}
```

**Use Case:** Process recorded client conversations for investment insights.

### 3. PDF Document Analysis

**Request:**
```json
{
  "message": "Summarize the key findings from this compliance report",
  "contents": [
    {
      "type": "file",
      "data": "data:application/pdf;base64,JVBERi0xLjQK...",
      "mediaType": "application/pdf",
      "fileName": "compliance_report_q4.pdf"
    }
  ]
}
```

**Use Case:** Analyze regulatory documents, financial reports, or investment prospectuses.

### 4. External URL Reference

**Request:**
```json
{
  "message": "Analyze the company mentioned in this article",
  "contents": [
    {
      "type": "uri",
      "uri": "https://example.com/market-analysis/tech-stocks-2024.html"
    }
  ]
}
```

**Use Case:** Reference external resources like news articles or financial data.

### 5. Mixed Content (Text + Image + Document)

**Request:**
```json
{
  "message": "Compare the chart with the recommendations in the attached PDF",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgo...",
      "mediaType": "image/png"
    },
    {
      "type": "file",
      "data": "data:application/pdf;base64,JVBERi0xLjQK...",
      "mediaType": "application/pdf",
      "fileName": "analyst_recommendations.pdf"
    }
  ]
}
```

**Use Case:** Multi-source analysis combining visual data and text documents.

## SignalR Streaming

For real-time streaming responses, use the `ChatStreamMultiModal` hub method:

```typescript
// TypeScript/JavaScript example
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/masteragent")
  .build();

await connection.start();

const request = {
  message: "Analyze this portfolio chart",
  contents: [
    {
      type: "image",
      data: "data:image/png;base64,...",
      mediaType: "image/png"
    }
  ],
  conversationId: "conv-123"
};

await connection.stream("ChatStreamMultiModal", request)
  .subscribe({
    next: (response) => {
      console.log("Chunk:", response.content);
    },
    complete: () => {
      console.log("Streaming complete");
    },
    error: (err) => {
      console.error("Error:", err);
    }
  });
```

## Content Validation Rules

### Text Content
- Must have `text` property with non-empty string

### Image Content
- Must have `data` (base64/data URI) OR `uri` property
- Recommended `mediaType`: `image/png`, `image/jpeg`, `image/gif`, `image/webp`

### Audio Content
- Must have `data` (base64/data URI) OR `uri` property
- Recommended `mediaType`: `audio/wav`, `audio/mp3`, `audio/mpeg`, `audio/ogg`

### File Content
- Must have `data` (base64 encoded) property
- Recommended `mediaType`: `application/pdf`, `text/plain`, `application/json`

### URI Content
- Must have `uri` property with valid URL

## Base64 Encoding

### Data URI Format
```
data:<mediaType>;base64,<base64EncodedData>
```

### Example (JavaScript)
```javascript
// Convert file to base64 data URI
function fileToDataUri(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

// Usage
const imageFile = document.getElementById('fileInput').files[0];
const dataUri = await fileToDataUri(imageFile);

const request = {
  message: "Analyze this image",
  contents: [
    {
      type: "image",
      data: dataUri,
      mediaType: imageFile.type
    }
  ]
};
```

### Example (C#)
```csharp
// Convert file to base64 data URI
byte[] fileBytes = await File.ReadAllBytesAsync("chart.png");
string base64 = Convert.ToBase64String(fileBytes);
string dataUri = $"data:image/png;base64,{base64}";

var request = new MultiModalChatRequest
{
    Message = "Analyze this chart",
    Contents = new List<ContentInput>
    {
        new ContentInput
        {
            Type = "image",
            Data = dataUri,
            MediaType = "image/png"
        }
    }
};
```

## Testing with Postman

1. Import the collection: `docs/MasterAgent-Tests.postman_collection.json`
2. Add a new request:
   - **Method:** POST
   - **URL:** `http://localhost:5000/api/v2/MasterAgent/chat/multimodal`
   - **Body (raw JSON):**
   ```json
   {
     "message": "What's in this image?",
     "contents": [
       {
         "type": "image",
         "data": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
         "mediaType": "image/png"
       }
     ]
   }
   ```

## Model Requirements

**Vision-enabled models required for image analysis:**
- OpenAI: `gpt-4o`, `gpt-4-turbo`, `gpt-4-vision-preview`
- Azure OpenAI: `gpt-4o`, `gpt-4-turbo-v`

**Audio processing models:**
- OpenAI: `whisper-1` (transcription)
- GPT-4 models can analyze transcribed audio content

Configure your model in `appsettings.json` or `Program.cs`:
```json
{
  "OpenAI": {
    "Model": "gpt-4o"
  }
}
```

## Architecture

```
Client Request (JSON)
    ↓
ContentInput DTOs (ApiModels.cs)
    ↓
ContentConverter.ConvertToAIContents()
    ↓
List<AIContent> (Microsoft.Extensions.AI)
    ↓
ChatMessage(ChatRole.User, contents)
    ↓
MasterOrchestrator.ProcessMultiModalRequestAsync()
    ↓
Agent.RunAsync(chatMessage)
    ↓
Response with analysis
```

## Limitations

- **File Size:** Recommended max 20MB for images, 25MB for audio files
- **Base64 Overhead:** Base64 encoding increases size by ~33%
- **Request Timeout:** Large files may require increased timeout settings
- **Token Limits:** Images consume significant tokens (e.g., ~765 tokens for 512x512px)
- **Model Support:** Not all models support all content types - use vision/audio-capable models

## Error Handling

**Common Errors:**

| Error | Cause | Solution |
|-------|-------|----------|
| "Content type must be specified" | Missing `type` field | Add `type` property to content |
| "Image data or URI required" | No `data` or `uri` | Provide base64 data or URL |
| "Invalid base64 data" | Malformed encoding | Verify base64 encoding is correct |
| "Unsupported media type" | Unknown MIME type | Use standard MIME types |

**Example Error Response:**
```json
{
  "success": false,
  "data": null,
  "error": "Image content must have either data (base64/data URI) or uri property"
}
```

## Next Steps

1. **Frontend Integration:** Create file upload UI components (see `frontend/src/components/`)
2. **Testing:** Use provided Postman collection for API testing
3. **Model Configuration:** Ensure using vision-capable models (gpt-4o recommended)
4. **Monitoring:** Check OpenTelemetry traces for multi-modal request processing
