# Testing Multi-Modal API with Postman

## Quick Start Guide

This guide shows you how to test the multi-modal chat endpoint in Postman, including sending images, audio files, and documents to the Master Agent.

## Prerequisites

1. **Start the API server:**
   ```bash
   cd src
   dotnet run
   ```
   The API should be running on `http://localhost:5000`

2. **Verify the server is running:**
   - Open browser to `http://localhost:5000/swagger` (if Swagger is enabled)
   - Or test with: `http://localhost:5000/api/v2/MasterAgent/subagents`

3. **Important:** You need:
   - **`gpt-4o`** (vision-capable model) for images/documents
   - **`whisper-1`** (audio transcription) for audio files
   - Both configured via `OPENAI_API_KEY` environment variable

---

## 🎯 Recommended: Form-Data File Upload (Easiest!)

This is the **simplest method** - just upload files directly like you would in a web form. The API handles all conversion automatically, including audio transcription.

### Endpoint
```
POST http://localhost:5000/api/v2/MasterAgent/chat/multimodal/upload
```

### Step-by-Step in Postman

1. **Create a new POST request** with the URL above
2. **Go to Body tab** → Select **form-data**
3. **Add fields:**
   - `message` (text): "Analyze this image" or "Transcribe this audio"
   - `files` (file): Click dropdown → Select **File** → Choose your file(s)
   - `conversationId` (text): Optional UUID for conversation tracking

4. **Send** the request!

### Example 1: Upload an Image

| Key | Type | Value |
|-----|------|-------|
| `message` | Text | `What's in this image?` |
| `files` | File | `chart.png` |

**Response:**
```json
{
    "success": true,
    "data": {
        "message": "I can see a bar chart showing quarterly sales data...",
        "contentTypesProcessed": ["image"],
        "conversationId": "uuid-here",
        "processingTimeMs": 2431
    }
}
```

### Example 2: Upload an Audio File

| Key | Type | Value |
|-----|------|-------|
| `message` | Text | `What does the speaker say?` |
| `files` | File | `recording.mp3` |

**How it works:**
1. API detects it's an audio file (MP3, WAV, M4A, etc.)
2. Calls OpenAI Whisper API to transcribe audio → text
3. Sends transcript to `gpt-4o` with your message
4. Returns the AI's response

**Response:**
```json
{
    "success": true,
    "data": {
        "message": "[Audio transcription from recording.mp3]: Hello, this is a test message about quarterly results...\n\nThe speaker discusses quarterly financial results and mentions...",
        "contentTypesProcessed": ["audio"],
        "conversationId": "uuid-here",
        "processingTimeMs": 4521
    }
}
```

### Example 3: Multiple Files

| Key | Type | Value |
|-----|------|-------|
| `message` | Text | `Compare these` |
| `files` | File | `image1.png` |
| `files` | File | `image2.png` |

**Supported file types:**
- **Images:** PNG, JPG, JPEG, GIF, WebP, SVG, BMP
- **Audio:** MP3, WAV, M4A, WebM, OGG, FLAC (transcribed via Whisper)
- **Documents:** PDF (sent as binary data)

---

## Method 1: Simple Image Testing (Recommended for Quick Tests)

### Step 1: Create a New POST Request

1. Open Postman
2. Click **New** → **HTTP Request**
3. Set method to **POST**
4. Enter URL: `http://localhost:5000/api/v2/MasterAgent/chat/multimodal`

### Step 2: Set Headers

Go to the **Headers** tab and add:
```
Content-Type: application/json
```

### Step 3: Prepare Your Request Body

Go to the **Body** tab:
1. Select **raw**
2. Choose **JSON** from the dropdown

### Step 4: Use a Small Test Image

Copy and paste this JSON (includes a tiny 1x1 red pixel PNG):

```json
{
  "message": "What color is this image? Please describe what you see.",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==",
      "mediaType": "image/png"
    }
  ],
  "conversationId": "test-session-1"
}
```

### Step 5: Send Request

Click **Send** button.

### Expected Response

You should receive a JSON response like:

```json
{
  "success": true,
  "data": {
    "message": "This is a red colored image. It appears to be a single pixel...",
    "timestamp": "2024-01-15T10:30:00Z",
    "processingTimeMs": 1250,
    "contentTypesProcessed": ["image"],
    "conversationId": "test-session-1"
  },
  "error": null
}
```

## Method 2: Upload Your Own Images

### ⭐ Recommended: Using Online Base64 Converter

**This is the most reliable method that works in all Postman versions.**

1. **Convert your image to base64:**
   - Go to: **https://www.base64-image.de/**
   - Click "Choose File" and select your image (PNG, JPEG, GIF)
   - Click **"Copy image"** button
   - The full data URI (including `data:image/...;base64,...`) is now in your clipboard

2. **Use in Postman:**
   - Method: **POST**
   - URL: `http://localhost:5000/api/v2/MasterAgent/chat/multimodal`
   - Body → **raw** → **JSON**
   
   ```json
   {
     "message": "Analyze this chart and provide investment insights",
     "contents": [
       {
         "type": "image",
         "data": "PASTE_YOUR_COPIED_BASE64_STRING_HERE",
         "mediaType": "image/png",
         "fileName": "my-chart.png"
       }
     ]
   }
   ```

3. **Send the request**

---

### Option B: Using Shell Script (Linux/Mac/Git Bash)

**Automated conversion using bash script:**

Use the included script at `scripts/convert-image-to-json.sh`:

```bash
# Make executable (first time only)
chmod +x scripts/convert-image-to-json.sh

# Convert and copy to clipboard (Linux)
./scripts/convert-image-to-json.sh path/to/image.png | xclip -selection clipboard

# Convert and copy to clipboard (Mac)
./scripts/convert-image-to-json.sh path/to/image.png | pbcopy

# Convert and copy to clipboard (Windows Git Bash)
./scripts/convert-image-to-json.sh path/to/image.png | clip

# Save to file
./scripts/convert-image-to-json.sh path/to/image.png > request.json

# With custom message
./scripts/convert-image-to-json.sh chart.png "Analyze investment trends" | pbcopy
```

Then paste the JSON into Postman Body → raw → JSON.

**Script supports:** PNG, JPEG, GIF, WebP, BMP, SVG, PDF, MP3, WAV

---

### Option C: Using PowerShell Script (Windows)

**If you want to automate conversion locally:**

Save this as `convert-image-to-json.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$ImagePath,
    
    [string]$Message = "Analyze this image"
)

if (-not (Test-Path $ImagePath)) {
    Write-Error "File not found: $ImagePath"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($ImagePath)
$base64 = [Convert]::ToBase64String($bytes)

$ext = [System.IO.Path]::GetExtension($ImagePath).ToLower()
$mediaType = switch ($ext) {
    ".png"  { "image/png" }
    ".jpg"  { "image/jpeg" }
    ".jpeg" { "image/jpeg" }
    ".gif"  { "image/gif" }
    ".webp" { "image/webp" }
    default { "image/png" }
}

$dataUri = "data:$mediaType;base64,$base64"
$fileName = [System.IO.Path]::GetFileName($ImagePath)

$json = @{
    message = $Message
    contents = @(
        @{
            type = "image"
            data = $dataUri
            mediaType = $mediaType
            fileName = $fileName
        }
    )
} | ConvertTo-Json -Depth 5

Write-Output $json
```

**Usage:**

```powershell
# Copy JSON to clipboard
.\convert-image-to-json.ps1 "C:\charts\my-chart.png" | clip

# Or save to file
.\convert-image-to-json.ps1 "C:\charts\my-chart.png" > request.json

# With custom message
.\convert-image-to-json.ps1 "C:\charts\my-chart.png" -Message "What trends do you see?" | clip
```

Then paste the JSON into Postman Body → raw → JSON.

---

### ⚠️ Option C: Postman Pre-request Script (Legacy - Not Recommended)

### ⚠️ Option C: Postman Pre-request Script (Legacy - Not Recommended)

**Note:** Newer Postman Desktop versions (v10+) run scripts in a restricted sandbox that blocks `require('fs')` for security. This method may not work in your Postman version. **Use the online converter method instead.**

<details>
<summary>Click to expand legacy pre-request script (for older Postman versions only)</summary>

This method **only works in old Postman Desktop versions** (pre-v10) that allow unrestricted Node.js `fs` access.

1. **Save your image file** locally (e.g., `C:\images\chart.png`)

2. **Go to Pre-request Script tab** in Postman

3. **Paste this script:**

```javascript
// This script reads a file and converts it to base64
// Note: This requires Postman Desktop App (not web version)

const fs = require('fs');
const path = require('path');

// CHANGE THIS PATH to your image file
const imagePath = 'C:\\images\\chart.png';

try {
    // Read the file
    const imageBuffer = fs.readFileSync(imagePath);
    
    // Convert to base64
    const base64Image = imageBuffer.toString('base64');
    
    // Detect media type from extension
    const ext = path.extname(imagePath).toLowerCase();
    let mediaType = 'image/png';
    if (ext === '.jpg' || ext === '.jpeg') mediaType = 'image/jpeg';
    if (ext === '.gif') mediaType = 'image/gif';
    if (ext === '.webp') mediaType = 'image/webp';
    
    // Create data URI
    const dataUri = `data:${mediaType};base64,${base64Image}`;
    
    // Store in environment variable
    pm.environment.set('imageDataUri', dataUri);
    pm.environment.set('imageMediaType', mediaType);
    
    console.log('✅ Image converted successfully');
    console.log('Media Type:', mediaType);
    console.log('Size:', Math.round(base64Image.length / 1024), 'KB');
    
} catch (error) {
    console.error('❌ Error reading image:', error.message);
}
```

4. **Update your request body to use the variable:**

```json
{
  "message": "Analyze this portfolio chart",
  "contents": [
    {
      "type": "image",
      "data": "{{imageDataUri}}",
      "mediaType": "{{imageMediaType}}"
    }
  ]
}
```

5. **Click Send** - The script runs automatically before the request

**⚠️ Known Issues:**
- **Postman v10+:** Script fails with "fs.readFileSync is not a function" due to sandbox restrictions
- **Workaround:** Use the online converter method (recommended) or PowerShell script above

</details>

---

You can send multiple images in one request:

```json
{
  "message": "Compare these two charts and provide insights",
  "contents": [
    {
      "type": "text",
      "text": "Here are the Q3 and Q4 performance charts:"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgo...",
      "mediaType": "image/png",
      "fileName": "q3_chart.png"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgo...",
      "mediaType": "image/png",
      "fileName": "q4_chart.png"
    }
  ]
}
```

## Method 4: Image from URL

If your image is hosted online, you can use the URI type:

```json
{
  "message": "Analyze this stock chart",
  "contents": [
    {
      "type": "uri",
      "uri": "https://example.com/images/stock-chart.png"
    }
  ]
}
```

**Note:** The AI model will attempt to fetch the image from the URL.

## Testing Different Content Types

### Audio File Example

```json
{
  "message": "Transcribe and analyze this audio recording",
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

### PDF Document Example

```json
{
  "message": "Summarize this compliance report",
  "contents": [
    {
      "type": "file",
      "data": "data:application/pdf;base64,JVBERi0xLjQK...",
      "mediaType": "application/pdf",
      "fileName": "compliance_report.pdf"
    }
  ]
}
```

### Mixed Content Example

```json
{
  "message": "Analyze the portfolio based on this chart and document",
  "contents": [
    {
      "type": "text",
      "text": "Portfolio Analysis Request for Client ABC123"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgo...",
      "mediaType": "image/png",
      "fileName": "portfolio_chart.png"
    },
    {
      "type": "file",
      "data": "data:application/pdf;base64,JVBERi0xLjQK...",
      "mediaType": "application/pdf",
      "fileName": "recommendations.pdf"
    }
  ]
}
```

## Postman Collection Setup

### Create a Collection

1. **New Collection:**
   - Click **Collections** → **New Collection**
   - Name it: "Multi-Modal Master Agent Tests"

2. **Add Collection Variables:**
   - Go to collection **Variables** tab
   - Add these variables:

   | Variable | Initial Value | Current Value |
   |----------|---------------|---------------|
   | `baseUrl` | `http://localhost:5000` | `http://localhost:5000` |
   | `apiVersion` | `v2` | `v2` |

3. **Use variables in requests:**
   ```
   {{baseUrl}}/api/{{apiVersion}}/MasterAgent/chat/multimodal
   ```

### Sample Requests to Add

Create these requests in your collection:

#### 1. Simple Image Analysis
**POST** `{{baseUrl}}/api/{{apiVersion}}/MasterAgent/chat/multimodal`

**Body:**
```json
{
  "message": "What's in this image?",
  "contents": [
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==",
      "mediaType": "image/png"
    }
  ]
}
```

#### 2. Investment Chart Analysis
**POST** `{{baseUrl}}/api/{{apiVersion}}/MasterAgent/chat/multimodal`

**Body:**
```json
{
  "message": "Analyze this portfolio performance chart and provide investment recommendations",
  "contents": [
    {
      "type": "image",
      "data": "{{portfolioChartBase64}}",
      "mediaType": "image/png",
      "fileName": "portfolio_q4.png"
    }
  ],
  "conversationId": "investment-analysis-{{$timestamp}}"
}
```

#### 3. Multi-Image Comparison
**POST** `{{baseUrl}}/api/{{apiVersion}}/MasterAgent/chat/multimodal`

**Body:**
```json
{
  "message": "Compare these two portfolio charts and identify key differences",
  "contents": [
    {
      "type": "image",
      "data": "{{chart1Base64}}",
      "mediaType": "image/png",
      "fileName": "before.png"
    },
    {
      "type": "image",
      "data": "{{chart2Base64}}",
      "mediaType": "image/png",
      "fileName": "after.png"
    }
  ]
}
```

## Troubleshooting

### Error: "Request must include at least one content item"

**Cause:** Empty or missing `contents` array

**Solution:**
```json
{
  "message": "Your message",
  "contents": [
    {
      "type": "text",
      "text": "At least one content item required"
    }
  ]
}
```

### Error: "Image content must have either data or uri property"

**Cause:** Missing `data` or `uri` in image content

**Solution:** Ensure your image content has base64 data:
```json
{
  "type": "image",
  "data": "data:image/png;base64,YOUR_BASE64_HERE",
  "mediaType": "image/png"
}
```

### Error: "Invalid base64 data"

**Cause:** Malformed base64 string

**Solutions:**
1. Ensure base64 string is complete (no truncation)
2. Check for proper data URI format: `data:image/png;base64,XXXXX`
3. Use online validator: https://base64.guru/standards/base64url

### Request Times Out

**Cause:** Image too large

**Solutions:**
1. Resize image before encoding (recommend max 2048x2048px)
2. Compress image (use JPEG instead of PNG for photos)
3. Increase request timeout in Postman:
   - Go to Settings → General
   - Increase "Request timeout in ms" (default 30000)

### Response: "Model does not support vision"

**Cause:** Using a non-vision model (e.g., `gpt-3.5-turbo`)

**Solution:** Update `appsettings.json` or environment variables:
```json
{
  "OpenAI": {
    "Model": "gpt-4o"
  }
}
```

Or set environment variable:
```bash
export OPENAI_MODEL=gpt-4o
```

## Real-World Testing Examples

### Example 1: Stock Chart Analysis

1. **Get a stock chart:**
   - Go to: https://finance.yahoo.com/quote/AAPL/chart
   - Take a screenshot
   - Save as `stock_chart.png`

2. **Convert to base64:**
   - Use https://www.base64-image.de/
   - Upload your screenshot
   - Copy the data URI

3. **Send to API:**
   ```json
   {
     "message": "Analyze this stock chart. Identify trends, support/resistance levels, and provide trading recommendations.",
     "contents": [
       {
         "type": "image",
         "data": "YOUR_BASE64_DATA_URI_HERE",
         "mediaType": "image/png"
       }
     ]
   }
   ```

### Example 2: Document + Chart Analysis

```json
{
  "message": "Compare the financial report with the performance chart and summarize key findings",
  "contents": [
    {
      "type": "file",
      "data": "data:application/pdf;base64,JVBERi0xLjQKJeLjz9MK...",
      "mediaType": "application/pdf",
      "fileName": "Q4_Financial_Report.pdf"
    },
    {
      "type": "image",
      "data": "data:image/png;base64,iVBORw0KGgoAAAANSU...",
      "mediaType": "image/png",
      "fileName": "Q4_Performance_Chart.png"
    }
  ]
}
```

## Best Practices

1. **Image Size:**
   - Keep images under 2MB for best performance
   - Use 1024x1024 or 2048x2048 max resolution
   - Compress images before encoding

2. **File Formats:**
   - **Images:** PNG, JPEG, GIF, WebP
   - **Audio:** WAV, MP3, OGG
   - **Documents:** PDF, TXT

3. **Testing Strategy:**
   - Start with small test images
   - Verify model configuration (gpt-4o)
   - Test one content type at a time
   - Then combine multiple content types

4. **Performance:**
   - Larger images = more tokens = slower response
   - A 1024x1024 image uses ~765 tokens
   - Consider batch processing for multiple images

5. **Error Handling:**
   - Always check `success` field in response
   - Look at `error` field for details
   - Check console logs on server for debugging

## Save Your Collection

1. **Export Collection:**
   - Right-click collection → **Export**
   - Choose "Collection v2.1"
   - Save as `MultiModal-Tests.postman_collection.json`

2. **Share with Team:**
   - Upload to Postman workspace, or
   - Commit to git repository: `docs/MultiModal-Tests.postman_collection.json`

## Next Steps

1. **Test with real images** from your investment platform
2. **Create saved examples** in your collection
3. **Set up environment variables** for different environments (dev, staging, prod)
4. **Integrate with frontend** using the same JSON structure
5. **Add authentication** headers if your API requires auth

## Additional Resources

- **Base64 Encoder:** https://www.base64-image.de/
- **Image Optimizer:** https://tinypng.com/
- **Postman Documentation:** https://learning.postman.com/
- **OpenAI Vision Guide:** https://platform.openai.com/docs/guides/vision

## Need Help?

Check the server logs when testing:
```bash
cd src
dotnet run

# You'll see logs like:
# info: MasterAgentController[0]
#       Master agent multi-modal chat request with 1 content items
```

Look for errors in the console output to debug issues.
