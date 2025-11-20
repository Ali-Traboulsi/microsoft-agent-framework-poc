#!/bin/bash

# Convert image to multi-modal JSON request for API testing
# Usage: ./convert-image-to-json.sh path/to/image.png ["Optional message"]
#        ./convert-image-to-json.sh path/to/image.png | clip  (copy to clipboard)

# Check if image path is provided
if [ -z "$1" ]; then
    echo "Error: Image path required" >&2
    echo "Usage: $0 <image-path> [message]" >&2
    echo "Example: $0 chart.png \"Analyze this chart\"" >&2
    exit 1
fi

IMAGE_PATH="$1"
MESSAGE="${2:-Analyze this image}"

# Check if file exists
if [ ! -f "$IMAGE_PATH" ]; then
    echo "Error: File not found: $IMAGE_PATH" >&2
    exit 1
fi

# Get filename
FILENAME=$(basename "$IMAGE_PATH")

# Detect media type from extension
EXT="${FILENAME##*.}"
EXT_LOWER=$(echo "$EXT" | tr '[:upper:]' '[:lower:]')

case "$EXT_LOWER" in
    png)
        MEDIA_TYPE="image/png"
        ;;
    jpg|jpeg)
        MEDIA_TYPE="image/jpeg"
        ;;
    gif)
        MEDIA_TYPE="image/gif"
        ;;
    webp)
        MEDIA_TYPE="image/webp"
        ;;
    bmp)
        MEDIA_TYPE="image/bmp"
        ;;
    svg)
        MEDIA_TYPE="image/svg+xml"
        ;;
    pdf)
        MEDIA_TYPE="application/pdf"
        ;;
    mp3)
        MEDIA_TYPE="audio/mpeg"
        ;;
    wav)
        MEDIA_TYPE="audio/wav"
        ;;
    *)
        MEDIA_TYPE="application/octet-stream"
        ;;
esac

# Convert to base64
BASE64_DATA=$(base64 < "$IMAGE_PATH" | tr -d '\n')

# Create data URI
DATA_URI="data:${MEDIA_TYPE};base64,${BASE64_DATA}"

# Determine content type for JSON
if [[ "$MEDIA_TYPE" == image/* ]]; then
    CONTENT_TYPE="image"
elif [[ "$MEDIA_TYPE" == audio/* ]]; then
    CONTENT_TYPE="audio"
else
    CONTENT_TYPE="file"
fi

# Generate JSON (escape message for JSON)
MESSAGE_ESCAPED=$(echo "$MESSAGE" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')
FILENAME_ESCAPED=$(echo "$FILENAME" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')

cat <<EOF
{
  "message": "$MESSAGE_ESCAPED",
  "contents": [
    {
      "type": "$CONTENT_TYPE",
      "data": "$DATA_URI",
      "mediaType": "$MEDIA_TYPE",
      "fileName": "$FILENAME_ESCAPED"
    }
  ]
}
EOF
