import { useState, useEffect, useRef, useCallback } from "react";
import {
  Database,
  Table,
  Key,
  Link as LinkIcon,
  Eye,
  EyeOff,
} from "lucide-react";

interface Column {
  name: string;
  type: string;
  isPrimary: boolean;
  foreignKeyContext?: string;
}

interface TableData {
  name: string;
  description: string;
  columns: Column[];
}

interface Edge {
  id: string;
  startX: number;
  startY: number;
  endX: number;
  endY: number;
}

export function SchemaViewerPage() {
  const [tables, setTables] = useState<TableData[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [edges, setEdges] = useState<Edge[]>([]);
  const [showEdges, setShowEdges] = useState(true);
  const containerRef = useRef<HTMLDivElement>(null);

  const drawEdges = useCallback(() => {
    if (!containerRef.current || tables.length === 0 || !showEdges) {
      if (!showEdges) setEdges([]);
      return;
    }
    const container = containerRef.current.getBoundingClientRect();
    const newEdges: Edge[] = [];

    tables.forEach((table) => {
      table.columns.forEach((col) => {
        if (col.foreignKeyContext) {
          // Expected format: targetTable(targetColumn)
          const match = col.foreignKeyContext.match(
            /([a-zA-Z0-9_]+)\(([a-zA-Z0-9_]+)\)/,
          );
          if (match) {
            const targetTable = match[1];
            const targetCol = match[2];

            const startId = `col-${table.name.toLowerCase()}-${col.name.toLowerCase()}`;
            const endId = `col-${targetTable.toLowerCase()}-${targetCol.toLowerCase()}`;

            const startEl = document.getElementById(startId);
            const endEl = document.getElementById(endId);

            if (startEl && endEl) {
              const startRect = startEl.getBoundingClientRect();
              const endRect = endEl.getBoundingClientRect();

              const isRight = startRect.left < endRect.left;

              const startX = isRight
                ? startRect.right - container.left
                : startRect.left - container.left;
              const startY =
                startRect.top + startRect.height / 2 - container.top;

              const offsetEnd = isRight ? -8 : 8;
              const endX =
                (isRight
                  ? endRect.left - container.left
                  : endRect.right - container.left) + offsetEnd;
              const endY = endRect.top + endRect.height / 2 - container.top;

              newEdges.push({
                id: `${startId}-to-${endId}`,
                startX,
                startY,
                endX,
                endY,
              });
            }
          }
        }
      });
    });

    setEdges(newEdges);
  }, [tables, showEdges]);

  useEffect(() => {
    fetch("http://localhost:5071/api/schema")
      .then((res) => res.json())
      .then((data) => {
        setTables(data);
        setIsLoading(false);
      })
      .catch((err) => {
        console.error("Failed to load database schema", err);
        setIsLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!isLoading) {
      const timer = setTimeout(drawEdges, 150);
      return () => clearTimeout(timer);
    }
  }, [isLoading, drawEdges]);

  useEffect(() => {
    window.addEventListener("resize", drawEdges);
    return () => window.removeEventListener("resize", drawEdges);
  }, [drawEdges]);

  const getPath = (
    startX: number,
    startY: number,
    endX: number,
    endY: number,
  ) => {
    const headLen = Math.abs(endX - startX) * 0.5;
    const distanceX = Math.max(headLen, 60);

    const isRight = startX < endX;
    const cp1x = isRight ? startX + distanceX : startX - distanceX;
    const cp2x = isRight ? endX - distanceX : endX + distanceX;

    return `M ${startX} ${startY} C ${cp1x} ${startY}, ${cp2x} ${endY}, ${endX} ${endY}`;
  };

  return (
    <div
      className="page-container fade-in"
      ref={containerRef}
      style={{ position: "relative" }}
    >
      {!isLoading && showEdges && edges.length > 0 && (
        <svg
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            width: "100%",
            height: "100%",
            pointerEvents: "none",
            zIndex: 10,
          }}
        >
          <defs>
            <marker
              id="arrowhead"
              markerWidth="8"
              markerHeight="6"
              refX="7"
              refY="3"
              orient="auto"
              markerUnits="strokeWidth"
            >
              <polygon points="0 0, 8 3, 0 6" fill="#60a5fa" />
            </marker>
          </defs>
          {edges.map((edge) => (
            <path
              key={edge.id}
              d={getPath(edge.startX, edge.startY, edge.endX, edge.endY)}
              fill="none"
              stroke="#60a5fa"
              strokeWidth="2"
              strokeDasharray="4 3"
              markerEnd="url(#arrowhead)"
              style={{ opacity: 0.65, transition: "all 0.3s ease-in-out" }}
            />
          ))}
        </svg>
      )}

      <div
        className="page-header"
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <div>
          <h1>
            <Database className="page-icon" /> Database Schema Viewer
          </h1>
          <p className="page-description">
            Explore the active database structure to understand available tables
            and relationships.
          </p>
        </div>
        {!isLoading && tables.length > 0 && (
          <button
            onClick={() => setShowEdges(!showEdges)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: "8px",
              padding: "8px 16px",
              backgroundColor: showEdges ? "#1e3a8a" : "#374151",
              color: "#fff",
              border: "none",
              borderRadius: "6px",
              cursor: "pointer",
              transition: "all 0.2s ease",
              zIndex: 20,
            }}
          >
            {showEdges ? <EyeOff size={18} /> : <Eye size={18} />}
            {showEdges ? "Hide Relations" : "Show Relations"}
          </button>
        )}
      </div>

      {isLoading ? (
        <div style={{ textAlign: "center", padding: "2rem" }}>
          Loading schema from database...
        </div>
      ) : (
        <div className="schema-grid">
          {tables.map((table) => (
            <div key={table.name} className="schema-card">
              <div className="schema-card-header">
                <Table className="table-icon" />
                <h3>{table.name}</h3>
              </div>
              <p className="schema-desc">Database table</p>

              <div className="column-list">
                <div className="column-list-header">
                  <span>Column Name</span>
                  <span>Data Type</span>
                </div>
                {table.columns.map((col) => (
                  <div
                    key={col.name}
                    className="column-item"
                    id={`col-${table.name.toLowerCase()}-${col.name.toLowerCase()}`}
                  >
                    <span
                      className="col-name"
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "6px",
                      }}
                    >
                      {col.isPrimary && (
                        <Key
                          size={14}
                          className="primary-key-icon"
                          style={{ color: "#fbbf24" }}
                        />
                      )}
                      {col.foreignKeyContext && (
                        <LinkIcon size={14} style={{ color: "#60a5fa" }} />
                      )}
                      {col.name}
                    </span>
                    <span
                      className="col-type"
                      style={{
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "flex-end",
                        textAlign: "right",
                      }}
                    >
                      <span>{col.type}</span>
                      {col.foreignKeyContext && (
                        <span
                          style={{
                            fontSize: "0.70rem",
                            color: "#9ca3af",
                            marginTop: "2px",
                          }}
                        >
                          → {col.foreignKeyContext}
                        </span>
                      )}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
