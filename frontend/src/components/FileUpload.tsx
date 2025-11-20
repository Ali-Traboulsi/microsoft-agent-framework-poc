import React, { useRef, useState } from 'react';

export interface UploadedFile {
  file: File;
  preview?: string; // For images
  id: string;
}

interface FileUploadProps {
  files: UploadedFile[];
  onFilesChange: (files: UploadedFile[]) => void;
  maxFiles?: number;
  acceptedTypes?: string;
}

export const FileUpload: React.FC<FileUploadProps> = ({
  files,
  onFilesChange,
  maxFiles = 5,
  acceptedTypes = 'image/*,audio/*,.pdf,.txt,.json'
}) => {
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    
    const droppedFiles = Array.from(e.dataTransfer.files);
    await processFiles(droppedFiles);
  };

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const selectedFiles = Array.from(e.target.files);
      await processFiles(selectedFiles);
    }
  };

  const processFiles = async (newFiles: File[]) => {
    if (files.length + newFiles.length > maxFiles) {
      alert(`Maximum ${maxFiles} files allowed`);
      return;
    }

    const uploadedFiles: UploadedFile[] = [];

    for (const file of newFiles) {
      const uploadedFile: UploadedFile = {
        file,
        id: `${Date.now()}-${Math.random()}`
      };

      // Generate preview for images
      if (file.type.startsWith('image/')) {
        uploadedFile.preview = await readFileAsDataURL(file);
      }

      uploadedFiles.push(uploadedFile);
    }

    onFilesChange([...files, ...uploadedFiles]);
  };

  const readFileAsDataURL = (file: File): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  };

  const removeFile = (id: string) => {
    onFilesChange(files.filter(f => f.id !== id));
  };

  const getFileIcon = (file: File) => {
    if (file.type.startsWith('image/')) return '🖼️';
    if (file.type.startsWith('audio/')) return '🎵';
    if (file.type === 'application/pdf') return '📄';
    if (file.type.includes('json')) return '📊';
    if (file.type.includes('text')) return '📝';
    return '📎';
  };

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  };

  return (
    <div className="space-y-3">
      {/* Drop Zone */}
      <div
        onDrop={handleDrop}
        onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onClick={() => fileInputRef.current?.click()}
        className={`
          border-2 border-dashed rounded-xl p-6 text-center cursor-pointer
          transition-all duration-200
          ${dragOver 
            ? 'border-blue-500 bg-blue-50 scale-105' 
            : 'border-gray-300 bg-gray-50 hover:border-blue-400 hover:bg-blue-50/50'
          }
        `}
      >
        <input
          ref={fileInputRef}
          type="file"
          multiple
          accept={acceptedTypes}
          onChange={handleFileSelect}
          className="hidden"
        />
        
        <div className="flex flex-col items-center gap-2">
          <div className="text-4xl">
            {dragOver ? '⬇️' : '📎'}
          </div>
          <div>
            <p className="font-medium text-gray-700">
              {dragOver ? 'Drop files here' : 'Click or drag files here'}
            </p>
            <p className="text-xs text-gray-500 mt-1">
              Images, Audio, PDFs, Text (max {maxFiles} files)
            </p>
          </div>
        </div>
      </div>

      {/* File List */}
      {files.length > 0 && (
        <div className="space-y-2">
          <p className="text-sm font-medium text-gray-700">
            Uploaded Files ({files.length}/{maxFiles})
          </p>
          
          <div className="grid grid-cols-1 gap-2">
            {files.map((uploadedFile) => (
              <div
                key={uploadedFile.id}
                className="flex items-center gap-3 p-3 bg-white border border-gray-200 rounded-lg hover:border-blue-300 transition-colors group"
              >
                {/* Preview or Icon */}
                <div className="flex-shrink-0">
                  {uploadedFile.preview ? (
                    <img
                      src={uploadedFile.preview}
                      alt={uploadedFile.file.name}
                      className="w-12 h-12 object-cover rounded border border-gray-200"
                    />
                  ) : (
                    <div className="w-12 h-12 flex items-center justify-center bg-gray-100 rounded border border-gray-200 text-2xl">
                      {getFileIcon(uploadedFile.file)}
                    </div>
                  )}
                </div>

                {/* File Info */}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-900 truncate">
                    {uploadedFile.file.name}
                  </p>
                  <p className="text-xs text-gray-500">
                    {formatFileSize(uploadedFile.file.size)}
                    {uploadedFile.file.type && (
                      <span className="ml-2 px-2 py-0.5 bg-gray-100 rounded text-xs">
                        {uploadedFile.file.type.split('/')[1]?.toUpperCase() || 'FILE'}
                      </span>
                    )}
                  </p>
                </div>

                {/* Remove Button */}
                <button
                  onClick={() => removeFile(uploadedFile.id)}
                  className="flex-shrink-0 w-8 h-8 flex items-center justify-center text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-full transition-colors opacity-0 group-hover:opacity-100"
                  title="Remove file"
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Supported Formats Info */}
      {files.length === 0 && (
        <div className="flex flex-wrap gap-2 justify-center">
          {[
            { icon: '🖼️', label: 'Images', types: 'PNG, JPG, GIF, WebP' },
            { icon: '🎵', label: 'Audio', types: 'MP3, WAV, M4A' },
            { icon: '📄', label: 'Documents', types: 'PDF, TXT' }
          ].map((format) => (
            <div
              key={format.label}
              className="flex items-center gap-2 px-3 py-1.5 bg-white border border-gray-200 rounded-lg text-xs"
            >
              <span className="text-lg">{format.icon}</span>
              <div>
                <div className="font-medium text-gray-700">{format.label}</div>
                <div className="text-gray-500">{format.types}</div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
