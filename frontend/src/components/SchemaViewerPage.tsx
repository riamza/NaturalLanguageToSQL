import { useState, useEffect } from "react";
import { Database, Table, Key, Link as LinkIcon } from "lucide-react";

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

export function SchemaViewerPage() {
  const [tables, setTables] = useState<TableData[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetch('http://localhost:5071/api/query/schema')
      .then(res => res.json())
      .then(data => {
        setTables(data);
        setIsLoading(false);
      })
      .catch(err => {
        console.error("Failed to load database schema", err);
        setIsLoading(false);
      });
  }, []);

  return (
    <div className="page-container fade-in">
      <div className="page-header">
        <h1>
          <Database className="page-icon" /> Database Schema Viewer
        </h1>
        <p className="page-description">Explore the active database structure to understand available tables and relationships.</p>
      </div>

      {isLoading ? (
        <div style={{ textAlign: 'center', padding: '2rem' }}>Loading schema from database...</div>
      ) : (
        <div className="schema-grid">
          {tables.map(table => (
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
                {table.columns.map(col => (
                  <div key={col.name} className="column-item">
                    <span className="col-name" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      {col.isPrimary && <Key size={14} className="primary-key-icon" style={{ color: '#fbbf24' }} />}
                      {col.foreignKeyContext && <LinkIcon size={14} style={{ color: '#60a5fa' }} />}
                      {col.name}
                    </span>
                    <span className="col-type" style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', textAlign: 'right' }}>
                      <span>{col.type}</span>
                      {col.foreignKeyContext && (
                        <span style={{ fontSize: '0.70rem', color: '#9ca3af', marginTop: '2px' }}>
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