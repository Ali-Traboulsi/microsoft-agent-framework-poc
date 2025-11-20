# Direct File Upload Guide (Form-Data)

## Overview 🎉

The **easiest way** to test multi-modal capabilities! Upload files directly using `multipart/form-data` - no base64 conversion needed. The API handles all conversion automatically.

## Quick Start

### Endpoint
```
POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload
Content-Type: multipart/form-data
```

### Postman Setup (3 Steps)

#### Step 1: Create Request
- Method: `POST`
- URL: `http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload`

#### Step 2: Configure Body
- Go to **Body** tab
- Select **form-data** (NOT raw JSON)

#### Step 3: Add Fields

| Key | Type | Value | Example |
|-----|------|-------|---------|
| `message` | Text | Your prompt | "What's in this image?" |
| `files` | **File** | Click "Select Files" | Choose `chart.png` |

**Important:** Change `files` dropdown from "Text" to **"File"**

#### Step 4: Send 🚀

Click **Send** and get instant analysis!

---

## Example Response

```json
{
    "success": true,
    "data": {
        "message": "The image shows a bar chart displaying quarterly sales data for 2024. The chart indicates strong growth in Q3 with approximately $2.5M in revenue...",
        "timestamp": "2025-11-20T10:15:30Z",
        "processingTimeMs": 2341,
        "contentTypesProcessed": ["image"],
        "conversationId": "abc-123-def-456"
    },
    "error": null
}
```

---

## Advanced Usage

### Multiple Files

Add multiple `files` fields in form-data:

```
files: [File] chart.png
files: [File] audio.mp3
files: [File] report.pdf
message: [Text] "Analyze these documents"
```

### With URIs

Combine uploads with external URIs:

```
files: [File] local-chart.png
uris: [Text] https://example.com/remote-image.jpg
message: [Text] "Compare these images"
```

### Conversation Continuity

Include `conversationId` to maintain context:

```
files: [File] followup-chart.png
message: [Text] "How does this compare to the previous chart?"
conversationId: [Text] abc-123-def-456
```

---

## Supported File Types

### Images
- PNG, JPG, JPEG, GIF, WebP, BMP, SVG
- Auto-detected MIME types
- Sent to vision-capable models (gpt-4o)

### Audio
- MP3, WAV, M4A, OGG, FLAC
- Auto-detected MIME types
- Transcribed and analyzed

### Documents
- PDF, TXT, JSON, XML, CSV
- Extracted and processed
- Content analyzed by AI

---

## File Size Limits

- **Maximum per request:** 100MB
- **Maximum per file:** No specific limit (constrained by total)
- **Recommended:** Keep files under 20MB for faster processing

For larger files, consider:
1. Compressing images/audio before upload
2. Using URIs pointing to external files
3. Splitting into multiple requests

---

## Error Handling

### No Files Provided
```json
{
    "success": false,
    "data": null,
    "error": "Request must include at least one file or URI"
}
```

**Fix:** Add at least one file or URI field

### File Too Large
```json
{
    "success": false,
    "data": null,
    "error": "Request body too large"
}
```

**Fix:** Reduce file size or split into multiple requests

### Unsupported File Type
The API accepts all file types but may not process unknown formats optimally. Stick to documented types for best results.

---

## Frontend Integration

### React Example with Fetch

```typescript
async function uploadFileToAgent(file: File, message: string) {
    const formData = new FormData();
    formData.append('files', file);
    formData.append('message', message);

    const response = await fetch('http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload', {
        method: 'POST',
        body: formData, // Don't set Content-Type header - browser handles it
    });

    const result = await response.json();
    return result.data;
}

// Usage
const fileInput = document.querySelector('input[type="file"]');
const file = fileInput.files[0];
const response = await uploadFileToAgent(file, "Analyze this image");
console.log(response.message);
```

### React Component Example

```tsx
import { useState } from 'react';

function FileUploadChat() {
    const [file, setFile] = useState<File | null>(null);
    const [message, setMessage] = useState('');
    const [response, setResponse] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!file) return;

        setLoading(true);
        const formData = new FormData();
        formData.append('files', file);
        formData.append('message', message);

        try {
            const res = await fetch('http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload', {
                method: 'POST',
                body: formData,
            });

            const data = await res.json();
            if (data.success) {
                setResponse(data.data.message);
            } else {
                setResponse(`Error: ${data.error}`);
            }
        } catch (error) {
            setResponse(`Error: ${error.message}`);
        } finally {
            setLoading(false);
        }
    };

    return (
        <form onSubmit={handleSubmit}>
            <input
                type="file"
                accept="image/*,audio/*,.pdf"
                onChange={(e) => setFile(e.target.files?.[0] || null)}
            />
            <input
                type="text"
                placeholder="Your message..."
                value={message}
                onChange={(e) => setMessage(e.target.value)}
            />
            <button type="submit" disabled={!file || loading}>
                {loading ? 'Analyzing...' : 'Send'}
            </button>
            {response && <div className="response">{response}</div>}
        </form>
    );
}
```

### Axios Example

```typescript
import axios from 'axios';

async function uploadWithAxios(file: File, message: string) {
    const formData = new FormData();
    formData.append('files', file);
    formData.append('message', message);

    const { data } = await axios.post(
        'http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload',
        formData,
        {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        }
    );

    return data;
}
```

---

## Comparison with Base64 JSON Method

| Feature | Form-Data Upload ⭐ | Base64 JSON |
|---------|---------------------|-------------|
| **Ease of Use** | Very Easy | Complex |
| **Postman Setup** | 3 steps | Manual conversion needed |
| **Frontend Code** | Simple FormData | Complex base64 encoding |
| **File Size** | Efficient | 33% larger (base64 overhead) |
| **MIME Detection** | Automatic | Manual |
| **Best For** | File uploads, frontend forms | Embedded small images |
| **Streaming** | ❌ Use HTTP endpoint | ✅ Works with SignalR |

**Recommendation:** Use form-data upload for all file-based interactions unless you specifically need SignalR streaming, in which case convert to base64 on the frontend.

---

## SignalR Streaming Note

SignalR doesn't support `multipart/form-data`. For streaming responses with files:

1. **Option A (Recommended):** Use HTTP endpoint `/upload` for non-streaming responses
2. **Option B:** Convert files to base64 in frontend, then use existing SignalR method:

```typescript
// Convert file to base64 in frontend
const reader = new FileReader();
reader.readAsDataURL(file);
reader.onload = async () => {
    const base64Data = reader.result as string; // "data:image/png;base64,..."
    
    // Send via SignalR
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5000/hubs/masteragent")
        .build();
    
    await connection.start();
    
    const stream = connection.stream("ChatStreamMultiModal", {
        message: "Analyze this",
        contents: [{
            type: "image",
            data: base64Data,
            mediaType: "image/png"
        }]
    });
    
    for await (const chunk of stream) {
        console.log(chunk.content);
    }
};
```

---

## Testing Checklist

- [ ] API server running on `http://localhost:5000`
- [ ] Vision-capable model configured (`gpt-4o` in `appsettings.json`)
- [ ] Postman request set to `POST` method
- [ ] URL: `http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload`
- [ ] Body type set to **form-data**
- [ ] `files` field type changed to **File** (not Text)
- [ ] At least one file selected
- [ ] Optional `message` field added with prompt
- [ ] Send button clicked
- [ ] Response shows `"success": true`
- [ ] `data.message` contains AI analysis

---

## Troubleshooting

### "Request must include at least one file or URI"
- Ensure you've added a `files` field and set type to **File**
- Make sure you've clicked "Select Files" and chosen a file
- Check that the file actually uploaded (should show filename)

### "Request body too large"
- File exceeds 100MB limit
- Compress image/audio before upload
- Or split into multiple smaller requests

### "I'm unable to interpret images"
- Vision model not configured
- Check `appsettings.json` or environment: must use `gpt-4o`, not `gpt-4o-mini`
- Restart API after configuration changes

### Empty or Null Response
- Check console/logs for errors
- Verify API key is valid
- Ensure model has vision capabilities

### File Not Processing
- Verify file is not corrupted
- Check file extension matches actual format
- Try a different file to isolate issue

---

## Next Steps

1. ✅ Test with single image
2. ✅ Test with multiple files
3. ✅ Test with audio file
4. ✅ Test with PDF document
5. 🚀 Integrate into your React frontend
6. 🎨 Add drag-and-drop UI
7. 📊 Display processing results beautifully

For more information:
- [Multi-Modal Usage Guide](./MULTIMODAL_USAGE.md) - Comprehensive API documentation
- [Quick Reference](./MULTIMODAL_QUICK_REFERENCE.md) - Cheat sheet
- [Legacy Testing Guide](./POSTMAN_MULTIMODAL_TESTING.md) - Base64 JSON method
