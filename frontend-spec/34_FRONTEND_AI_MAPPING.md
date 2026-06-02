# 34 — Frontend-AI Service Mapping

## AI Intents → UI Behavior

| Intent (AI classifies to) | Triggered By | UI Response |
|--------------------------|-------------|-------------|
| `general_chat` | Any general message | Display AI response as markdown |
| `academic_coach` | "ايه وضعي؟", "analyze my grades" | Response with grade data, suggestions |
| `backend_api_query` | "كم مادة مسجل؟", "who am I" | AI fetches from backend, returns data |
| `generate_exam` | "اعمل امتحان" | AI generates exam, prompts to save |
| `result_query` | "ايه معدلي؟" | GPA/grades displayed in response |
| `study_plan` | "اعمللي خطة مذاكرة" | Study plan shown as structured list |
| `regulation` | "ايه اللائحة" | Regulation content from PDF |
| `quiz_me` | "سألني", "quiz me" | AI sends question → student answers |
| `generate_flashcards` | "اعمل flashcards" | Deck created, navigate to flashcards |
| `academic_advice` | "ازاي أحسن معدلي" | Personalized academic advice |
| `complaint_submit` | "عايز أشتكي" | Complaint form pre-filled |
| `material_explanation` | "شرحلي المادة" | Material summary with RAG |
| `assignment_query` | "واجباتي", "when is due" | Assignment details |
| `action_execute` | "سجلني في المواد" | Enrollment action → confirm modal |
| `academic_coach` (doctor) | Doctor's class analysis questions | Teaching intelligence data |
| `doctor_analytics` | "show class performance" | Teaching intelligence dashboard data |
| `progress_report` | "تقرير الأسبوع" | Weekly/monthly report narrative |

---

## FastAPI vs .NET API Decision

| Feature | Endpoint | Service |
|---------|----------|---------|
| AI Chat / Conversation | `POST /api/Chat/messages` | .NET (forwards to FastAPI) |
| Flashcard generation | `POST /api/companion/flashcards/generate` | .NET (calls FastAPI internally) |
| Study plan generation | `GET /api/companion/dashboard` (todayRecommendations) | .NET |
| Quick AI prompt | `POST /api/companion/sessions/complete` (feedback) | .NET (calls FastAPI) |
| Exam generation | `POST /api/exams/generate-ai` | .NET (calls FastAPI) |
| Assignment AI grading | `POST /api/assignments/submissions/{id}/ai-grade` | .NET (calls FastAPI) |
| RAG search | `POST /api/rag/search` | .NET (calls FastAPI) |

Frontend NEVER calls FastAPI directly — always goes through .NET backend which handles auth and orchestration.

---

## AI Response Rendering

All AI responses are markdown. Use `react-markdown`:
```tsx
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

<ReactMarkdown
  remarkPlugins={[remarkGfm]}
  components={{
    table: ({ children }) => <table className="border-collapse w-full">{children}</table>,
    th: ({ children }) => <th className="border px-3 py-2 bg-muted font-medium">{children}</th>,
    td: ({ children }) => <td className="border px-3 py-2">{children}</td>,
    code: ({ children }) => <code className="bg-muted px-1 rounded text-sm font-mono">{children}</code>,
    ul: ({ children }) => <ul className="list-disc list-inside space-y-1">{children}</ul>,
    strong: ({ children }) => <strong className="font-semibold">{children}</strong>,
  }}
>
  {aiResponseText}
</ReactMarkdown>
```

---

## Companion Intent → API Flow

```
Student sends: "اعمل flashcards على SQL"

1. Frontend: POST /api/Chat/messages { message: "اعمل flashcards على SQL", ... }
2. .NET → FastAPI: classifies intent = "generate_flashcards"
3. FastAPI: generates flashcard JSON
4. .NET: saves flashcard deck to DB via AiCompanionService
5. .NET: returns AI response with "Deck created! View your flashcards."
6. Frontend: receives response
7. Frontend: also receives `data.deck_id` in response
8. Frontend: navigates to /student/companion/flashcards/{deckId}
```

Note: The chat response may include structured `data` in the `Metadata` field.
