import React, { useRef, useEffect } from "react";
import { Search, Upload } from "lucide-react";

interface SearchFormProps {
  prompt: string;
  setPrompt: (value: string) => void;
  loading: boolean;
  onSearch: (e?: React.FormEvent) => void;
  onFileUpload: (e: React.ChangeEvent<HTMLInputElement>) => void;
}

export function SearchForm({
  prompt,
  setPrompt,
  loading,
  onSearch,
  onFileUpload,
}: SearchFormProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height =
        Math.min(textareaRef.current.scrollHeight, 200) + "px";
    }
  }, [prompt]);

  return (
    <form className="search-box" onSubmit={onSearch}>
      <div className="input-group">
        <Search className="search-icon" size={20} />
        <textarea
          placeholder="e.g. Find all IT employees earning more than 50000"
          value={prompt}
          ref={textareaRef}
          onChange={(e) => setPrompt(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              if (!loading && prompt.trim()) {
                onSearch(e as unknown as React.FormEvent);
              }
            }
          }}
          disabled={loading}
          rows={1}
          style={{
            resize: "none",
            overflowY: "auto",
            paddingTop: "12px",
            paddingBottom: "12px",
          }}
        />
        <button type="submit" disabled={loading || !prompt.trim()}>
          {loading ? "Searching..." : "Ask Database"}
        </button>

        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={loading}
          style={{
            background: "var(--bg-card)",
            color: "var(--text-main)",
            border: "1px solid var(--border)",
            marginLeft: "0.5rem",
          }}
          title="Upload CSV for insertion"
        >
          <Upload size={18} />
        </button>
        <input
          type="file"
          accept=".csv"
          ref={fileInputRef}
          style={{ display: "none" }}
          onChange={(e) => {
            onFileUpload(e);
            if (fileInputRef.current) fileInputRef.current.value = "";
          }}
        />
      </div>
    </form>
  );
}
