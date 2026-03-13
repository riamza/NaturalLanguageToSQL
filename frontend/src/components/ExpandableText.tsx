import { useState, useRef, useLayoutEffect } from "react";

export interface ExpandableTextProps {
  text: string;
  maxLines?: number;
  className?: string;
}

export function ExpandableText({
  text,
  maxLines = 10,
  className = "",
}: ExpandableTextProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [isTruncatable, setIsTruncatable] = useState(false);
  const textRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    if (!textRef.current) return;
    const { scrollHeight, clientHeight } = textRef.current;
    if (scrollHeight > clientHeight) {
      setIsTruncatable(true);
    }
  }, [text, maxLines]);

  return (
    <div className={`expandable-text-container ${className}`}>
      <div
        ref={textRef}
        style={{
          display: "-webkit-box",
          WebkitBoxOrient: "vertical",
          WebkitLineClamp: isExpanded ? "unset" : maxLines,
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "pre-wrap",
        }}
      >
        {text}
      </div>
      {isTruncatable && (
        <button
          className="expand-toggle-btn"
          onClick={(e) => {
            e.stopPropagation();
            setIsExpanded(!isExpanded);
          }}
          style={{
            background: "none",
            border: "none",
            color: "#0066cc",
            cursor: "pointer",
            padding: "4px 0",
            fontSize: "0.875rem",
          }}
        >
          {isExpanded ? "Show less" : "Show more"}
        </button>
      )}
    </div>
  );
}
