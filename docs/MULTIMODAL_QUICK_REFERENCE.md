# Multi-Modal API - Quick Reference

## 🚀 Quick Start

**Endpoint:** `POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal`

**Minimal Request:**
```json
{
  "message": "What's in this image?",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,YOUR_BASE64_HERE",
      "mediaType": "image/png"
    }
  ]
}
```

## 📸 How to Convert Images to Base64

### Online Tool (Easiest - Works Everywhere)
1. Go to: **https://www.base64-image.de/**
2. Upload your image
3. Copy the full data URI (includes `data:image/png;base64,...`)
4. Paste directly into `"data"` field

### Bash Script (Linux/Mac/Git Bash)
```bash
# Make executable
chmod +x scripts/convert-image-to-json.sh

# Copy full JSON to clipboard
./scripts/convert-image-to-json.sh image.png | pbcopy  # Mac
./scripts/convert-image-to-json.sh image.png | xclip -selection clipboard  # Linux
./scripts/convert-image-to-json.sh image.png | clip  # Windows Git Bash

# Save to file
./scripts/convert-image-to-json.sh image.png > request.json
```

### PowerShell (Windows)
```powershell
# Copy full JSON to clipboard
.\scripts\convert-image-to-json.ps1 "image.png" | clip

# Save to file
.\scripts\convert-image-to-json.ps1 "image.png" > request.json
```

### Command Line (Manual)
```bash
# Linux/Mac
base64 -i your_image.png

# Windows PowerShell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("your_image.png"))
```

### JavaScript (Browser)
```javascript
const input = document.querySelector('input[type="file"]');
const file = input.files[0];
const reader = new FileReader();
reader.onload = () => {
  const base64 = reader.result; // Includes data:image/...
  console.log(base64);
};
reader.readAsDataURL(file);
```

## 📋 Content Types Reference

| Type | Required Fields | Example Use |
|------|----------------|-------------|
| `text` | `text` | Plain text message |
| `image` | `data` OR `uri`, `mediaType` | Charts, screenshots |
| `audio` | `data` OR `uri`, `mediaType` | Voice recordings |
| `file` | `data`, `mediaType` | PDFs, documents |
| `uri` | `uri` | External URLs |

## 🎯 Common Scenarios

### 1️⃣ Single Image
```json
{
  "message": "Analyze this chart",
  "contents": [{
    "type": "image",
    "data": "data:image/png;base64,iVBORw0KGgo...",
    "mediaType": "image/png"
  }]
}
```

### 2️⃣ Multiple Images
```json
{
  "message": "Compare these",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,IMAGE1...",
      "mediaType": "image/png",
      "fileName": "before.png"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,IMAGE2...",
      "mediaType": "image/png",
      "fileName": "after.png"
    }
  ]
}
```

### 3️⃣ Text + Image
```json
{
  "contents": [
    {
      "type": "text",
      "text": "Portfolio analysis for Q4"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,...",
      "mediaType": "image/png"
    }
  ]
}
```

### 4️⃣ Image from URL
```json
{
  "message": "Analyze this",
  "contents": [{
    "type": "uri",
    "uri": "https://example.com/chart.png"
  }]
}
```

### 5️⃣ PDF + Image
```json
{
  "message": "Compare report with chart",
  "contents": [
    {
      "type": "file",
      "data": "data:application/pdf;base64,...",
      "mediaType": "application/pdf",
      "fileName": "report.pdf"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,...",
      "mediaType": "image/png"
    }
  ]
}
```

## ✅ Response Format

```json
{
  "success": true,
  "data": {
    "message": "AI analysis here...",
    "timestamp": "2024-01-15T10:30:00Z",
    "processingTimeMs": 1250,
    "contentTypesProcessed": ["image"],
    "conversationId": "abc-123"
  },
  "error": null
}
```

## ❌ Common Errors

| Error | Fix |
|-------|-----|
| "Request must include at least one content item" | Add items to `contents` array |
| "Image content must have either data or uri" | Add `data` or `uri` field |
| "Invalid base64 data" | Check base64 encoding |
| "Content type must be specified" | Add `type` field |
| Request timeout | Reduce image size |

## 🔧 Postman Setup

1. **Import Collection:**
   - File → Import
   - Select: `docs/MultiModal-Tests.postman_collection.json`

2. **Set Variables:**
   - Collection → Variables
   - `baseUrl`: `http://localhost:5000`
   - `apiVersion`: `v2`

3. **Test Requests:**
   - Start with "1. Simple Red Pixel Test"
   - Then "10. YOUR CUSTOM IMAGE HERE" with your own image

## 📏 Size Limits

| Item | Recommendation |
|------|----------------|
| **Image Resolution** | Max 2048x2048px |
| **File Size** | Under 2MB |
| **Base64 Overhead** | +33% file size |
| **Token Usage** | ~765 tokens per 512x512 image |

## 🎨 Media Types

### Images
- `image/png`
- `image/jpeg`
- `image/gif`
- `image/webp`

### Audio
- `audio/wav`
- `audio/mp3` or `audio/mpeg`
- `audio/ogg`

### Documents
- `application/pdf`
- `text/plain`
- `application/json`

## 🤖 Model Requirements

**Vision support required for images:**
- ✅ `gpt-4o` (recommended)
- ✅ `gpt-4-turbo`
- ✅ `gpt-4-vision-preview`
- ❌ `gpt-3.5-turbo` (no vision)

**Configure in `appsettings.json`:**
```json
{
  "OpenAI": {
    "Model": "gpt-4o"
  }
}
```

## 🔗 Related Endpoints

| Endpoint | Purpose |
|----------|---------|
| `POST /api/v2/MasterAgent/chat` | Regular text-only chat |
| `POST /api/v2/MasterAgent/chat/structured` | Structured responses |
| `POST /api/v2/MasterAgent/chat/multimodal` | Multi-modal (this endpoint) |
| `GET /api/v2/MasterAgent/subagents` | List available sub-agents |

## 📚 Documentation

- **Full Guide:** `docs/POSTMAN_MULTIMODAL_TESTING.md`
- **API Usage:** `docs/MULTIMODAL_USAGE.md`
- **Architecture:** `ARCHITECTURE.md`

## 🧪 Quick Test

**Copy this into Postman Body (raw JSON):**
```json
{
  "message": "What color is this?",
  "contents": [{
    "type": "image",
    "data": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==",
    "mediaType": "image/png"
  }]
}
```

**Expected:** AI should respond that it's a red image.

---

**Need Help?** Check server logs: `cd src && dotnet run`
