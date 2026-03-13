import {
  Sparkles,
  ShieldAlert,
  Cpu,
  WifiOff,
  Database,
  AlertCircle,
  Clock,
} from "lucide-react";

interface ErrorDisplayProps {
  error: string;
  prompt: string;
  loading: boolean;
  onRetry: (retryPrompt: string) => void;
}

export function ErrorDisplay({
  error,
  prompt,
  loading,
  onRetry,
}: ErrorDisplayProps) {
  if (!error) return null;

  // 1. Erori cu sugestie AI
  const separator = "\n\n**Sugestie AI:**";
  if (error.includes(separator)) {
    const [dbError, aiSuggestion] = error.split(separator);
    const cleanError = dbError.replace("Eroare SQL: ", "").trim();
    const cleanSuggestion = aiSuggestion.trim();

    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{ display: "flex", gap: "8px", alignItems: "center" }}
        >
          <Database
            size={20}
            className="text-red-500"
            style={{ flexShrink: 0 }}
          />
          <span>
            <strong>Eroare Bază de Date:</strong> {cleanError}
          </span>
        </div>
        <div className="error-ai">
          <div
            className="error-ai-text"
            style={{ display: "flex", gap: "8px", alignItems: "flex-start" }}
          >
            <Sparkles
              size={20}
              className="text-blue-500"
              style={{ flexShrink: 0 }}
            />
            <span>
              <strong>Sugestie AI:</strong> {cleanSuggestion}
            </span>
          </div>
          <button
            type="button"
            className="btn-retry"
            disabled={loading}
            onClick={() =>
              onRetry(
                `Cerința inițială: "${prompt}".\n\nExecuția a eșuat cu eroarea: "${cleanError}".\n\nTe rog refă query-ul ținând cont de această sugestie: "${cleanSuggestion}"`,
              )
            }
          >
            <Sparkles size={16} />
            Reface automat folosind sugestia
          </button>
        </div>
      </div>
    );
  }

  // 2. Limită atinsă pe API-ul de Inteligență Artificială (Groq/OpenAI)
  if (
    error.includes("rate_limit_exceeded") ||
    error.includes("Rate limit reached")
  ) {
    const limitMatch = error.match(
      /Please try again in (?:(\d+)m)?(?:([\d.]+)s)?/,
    );
    let timeMsg = "";

    if (limitMatch) {
      const minutes = limitMatch[1] ? parseInt(limitMatch[1], 10) : 0;
      const seconds = limitMatch[2] ? parseFloat(limitMatch[2]) : 0;

      const now = new Date();
      now.setMinutes(now.getMinutes() + minutes);
      now.setSeconds(now.getSeconds() + seconds);

      timeMsg = ` la ora ${now.toLocaleTimeString("ro-RO", { hour: "2-digit", minute: "2-digit" })}`;
    }

    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{
            display: "flex",
            gap: "10px",
            alignItems: "center",
            backgroundColor: "#fff3cd",
            color: "#856404",
            borderColor: "#ffeeba",
          }}
        >
          <Clock size={24} style={{ flexShrink: 0 }} />
          <span>
            <strong>Limită API atinsă:</strong> Ai depășit limita de interogări
            pentru asistentul gratuit. Te rugăm să încerci din nou{timeMsg} sau
            să contactezi administratorul.
          </span>
        </div>
      </div>
    );
  }

  // 3. Blocaje din sistemul de validare C# (Restricții de siguranță)
  if (
    error.includes("Unsafe or invalid intention detected") ||
    error.includes("Disallowed or non-existent column")
  ) {
    const cleanError = error
      .replace("Unsafe or invalid intention detected:", "")
      .trim();
    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{
            display: "flex",
            gap: "10px",
            alignItems: "center",
            backgroundColor: "#f8d7da",
            color: "#721c24",
          }}
        >
          <ShieldAlert size={24} style={{ flexShrink: 0 }} />
          <span>
            <strong>Interogare Blocată de Sistemul de Securitate:</strong>{" "}
            {cleanError}
          </span>
        </div>
      </div>
    );
  }

  // 4. Eroare AI Generică (De structură sau halucinație prompt, conexiune la Groq)
  if (
    error.includes("Eroare LLM") ||
    error.includes("Failed to translate natural language")
  ) {
    const cleanError = error
      .replace("Eroare LLM API:", "")
      .replace(
        "Failed to translate natural language to IR due to an LLM error:",
        "",
      )
      .trim();
    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{
            display: "flex",
            gap: "10px",
            alignItems: "center",
            backgroundColor: "#e2e3e5",
            color: "#383d41",
          }}
        >
          <Cpu size={24} style={{ flexShrink: 0 }} />
          <span>
            <strong>Eroare la Asistentul AI:</strong> Interogarea nu a putut fi
            procesată. {cleanError}
          </span>
        </div>
      </div>
    );
  }

  // 5. Eroare Conexiune (Când cade serverul .NET)
  if (
    error.includes("Network Error") ||
    error.includes("ERR_CONNECTION_REFUSED")
  ) {
    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{
            display: "flex",
            gap: "10px",
            alignItems: "center",
            backgroundColor: "#f8d7da",
            color: "#721c24",
          }}
        >
          <WifiOff size={24} style={{ flexShrink: 0 }} />
          <span>
            <strong>Lipsă Conexiune Server:</strong> Baza de date sau serverul
            central este offline. Verifică dacă backend-ul .NET rulează!
          </span>
        </div>
      </div>
    );
  }

  // 6. Eroare Bază de Date Standard pură
  if (
    error.includes("Eroare SQL:") ||
    error.toLowerCase().includes("database") ||
    error.includes("42P01")
  ) {
    return (
      <div className="error-panel">
        <div
          className="error-sys"
          style={{
            display: "flex",
            gap: "10px",
            alignItems: "center",
            backgroundColor: "#f8d7da",
            color: "#721c24",
          }}
        >
          <Database size={24} style={{ flexShrink: 0 }} />
          <span>
            <strong>Eroare Execuție SQL:</strong>{" "}
            {error.replace("Eroare SQL:", "").trim()}
          </span>
        </div>
      </div>
    );
  }

  // 7. Fallback / Generic
  return (
    <div className="error-panel" style={{ padding: 0 }}>
      <div
        className="error-sys default-error"
        style={{
          display: "flex",
          gap: "10px",
          alignItems: "center",
          margin: 0,
          border: "1px solid #f5c6cb",
        }}
      >
        <AlertCircle size={24} style={{ flexShrink: 0 }} />
        <span>
          <strong>A apărut o problemă:</strong> {error}
        </span>
      </div>
    </div>
  );
}
