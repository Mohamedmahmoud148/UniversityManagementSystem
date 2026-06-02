# 09 — AI Chat Platform

## Overview
The AI chat is a full-screen conversational interface available to ALL roles. It connects to the FastAPI AI service which classifies intent, routes to the appropriate module, and returns personalized responses with context awareness.

---

## Chat Page (`/chat`)

### Layout — Desktop
```
┌───────────────┬────────────────────────────────────────────┐
│  SIDEBAR       │ CONVERSATION AREA                          │
│  ─────────────│ ──────────────────────────────────────     │
│  [+ New Chat] │                                            │
│               │   AI COMPANION                             │
│  Recent:      │   ───────────────────────────────────     │
│  • Exam prep  │                                            │
│  • DB help    │   [AI]: مرحبا! أنا مساعدك الأكاديمي.      │
│  • Grades Q   │   ايه اللي تحتاج مساعدة فيه النهارده؟     │
│               │                                            │
│  ─────────────│   [User]: ايه وضعي في OS؟                │
│  History:     │                                            │
│  2 days ago   │   [AI]: 💭 جاري تحليل بياناتك...         │
│  • attendance │                      ⣾⣽⣻⢿⡿⣟⣯⣷ (spinner)  │
│               │                                            │
│               │   [AI]: وضعك في OS:                       │
│               │   • الحضور: 62% ⚠️ (تحت الـ 75%)         │
│               │   • درجة الـ Quiz الأخير: 45%             │
│               │   • محتاج مراجعة Chapter 4                │
│               │                                            │
│               │   [Quiz Me on OS] [Study Plan] [Explain]  │
│               │                                            │
│               │ ┌──────────────────────────────────────┐  │
│               │ │ اكتب رسالتك... (Ctrl+Enter to send)  │  │
│               │ │                              [📎] [↑] │  │
│               │ └──────────────────────────────────────┘  │
└───────────────┴────────────────────────────────────────────┘
```

### Mobile Layout
- Sidebar becomes a drawer (hamburger icon)
- Full-screen conversation view
- Floating chat input at bottom
- Suggestions as horizontal scrollable chips

---

## Conversation Management

### Create New Conversation
```typescript
// POST /api/Chat/conversations
const { data: conversation } = await createConversation({ 
  title: 'New Chat' 
});
// Navigate to /chat?id={conversation.id}
```

### Load Conversation History
```typescript
// GET /api/Chat/conversations/{id}/messages?page=1&size=50
// Paginated — load older messages on scroll up (infinite scroll up)
```

### Send Message Flow
```typescript
// POST /api/Chat/messages
const payload = {
  conversationId: activeConversationId,
  message: userInput,
};

// The .NET backend:
// 1. Saves user message to DB
// 2. Forwards to FastAPI /api/chat with full context
// 3. FastAPI classifies intent + routes to correct module
// 4. Returns AI response
// 5. .NET saves AI response to DB
// 6. Returns to frontend
```

---

## Message Types

### User Message
```jsx
<div className="flex justify-end mb-4">
  <div className="bg-primary text-primary-foreground rounded-2xl rounded-tr-sm px-4 py-2 max-w-xs">
    {message.content}
  </div>
</div>
```

### AI Message
```jsx
<div className="flex items-start gap-3 mb-4">
  <Avatar className="w-8 h-8 bg-gradient-to-br from-blue-500 to-purple-600">
    <AvatarFallback>AI</AvatarFallback>
  </Avatar>
  <div className="flex flex-col gap-2">
    <div className="bg-muted rounded-2xl rounded-tl-sm px-4 py-3 max-w-lg">
      <ReactMarkdown>{message.content}</ReactMarkdown>
    </div>
    {message.suggestions?.length > 0 && (
      <div className="flex gap-2 flex-wrap">
        {message.suggestions.map(s => (
          <Button variant="outline" size="sm" onClick={() => sendMessage(s)}>
            {s}
          </Button>
        ))}
      </div>
    )}
  </div>
</div>
```

### AI Typing Indicator
```jsx
<div className="flex items-center gap-2 px-4 py-2">
  <div className="flex gap-1">
    <span className="w-2 h-2 bg-primary rounded-full animate-bounce" />
    <span className="w-2 h-2 bg-primary rounded-full animate-bounce delay-100" />
    <span className="w-2 h-2 bg-primary rounded-full animate-bounce delay-200" />
  </div>
  <span className="text-sm text-muted-foreground">AI is thinking...</span>
</div>
```

---

## Supported Intents (Auto-detected by AI)

| Intent | What Happens | Example |
|--------|-------------|---------|
| `general_chat` | LLM responds directly | "مرحبا" |
| `academic_coach` | Fetches grades + attendance | "كيف وضعي الأكاديمي؟" |
| `backend_api_query` | Queries .NET backend | "كم مادة مسجل فيها؟" |
| `generate_exam` | Creates exam via AI | "اعمل امتحان في OS" |
| `result_query` | Fetches grades data | "ايه معدلي؟" |
| `study_plan` | Generates study schedule | "اعمللي خطة مذاكرة" |
| `regulation` | Searches regulation PDF | "ايه المواد في اللائحة؟" |
| `quiz_me` | Starts quiz session | "سألني على Databases" |
| `generate_flashcards` | Creates flashcard deck | "اعمل flashcards على الـ OS" |
| `academic_advice` | Personal advice | "ازاي أحسن معدلي؟" |
| `complaint_submit` | Submits complaint | "عايز أشتكي من الدكتور" |
| `material_explanation` | Explains course material | "شرحلي مادة الـ ML" |
| `assignment_query` | Shows assignment info | "امتى موعد تسليم الواجب؟" |
| `action_execute` | Executes action | "سجلني في المواد" |

---

## Context Injection

When sending a message to the AI, include full academic context:

```typescript
interface ChatPayload {
  conversationId: string;
  message: string;
  // These come from the user store:
  userId: string;
  role: string;
  history: Array<{ role: 'user' | 'assistant'; content: string }>;
  academicContext: {
    userId: string;
    studentId?: string;
    subjectOfferingId?: string;
    batchId?: string;
    batchName?: string;
    groupId?: string;
    departmentId?: string;
    departmentName?: string;
    collegeName?: string;
    subjectName?: string;
    // Doctor specific:
    profileId?: string;
  };
}
```

---

## Conversation State in Zustand

```typescript
interface ChatStore {
  conversations: Conversation[];
  activeConversationId: string | null;
  messages: Record<string, Message[]>;  // keyed by conversationId
  isTyping: boolean;
  
  setActiveConversation: (id: string) => void;
  addMessage: (conversationId: string, message: Message) => void;
  setTyping: (isTyping: boolean) => void;
}
```

---

## Role-Specific AI Behavior

| Role | AI Persona | Tone | Features |
|------|-----------|------|---------|
| Student | "مرشد" — academic companion | Warm, encouraging, Egyptian dialect | Academic coaching, quizzes, flashcards |
| Doctor | Peer-level academic advisor | Professional, analytical | Teaching analytics, exam generation |
| Admin | Chief of Staff | Executive, concise | System stats, user management |
| SuperAdmin | Senior technical advisor | Technical + strategic | All capabilities |

---

## Error States

| Error | Display |
|-------|---------|
| Network error | Toast: "Connection failed. Please try again." |
| AI service down | Banner: "AI service temporarily unavailable. Basic chat works." |
| Too long message | Inline: "Message too long (max 1000 characters)" |
| Rate limited | Toast: "Too many messages. Please wait 30 seconds." |

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Enter` | Send message |
| `Shift+Enter` | New line |
| `Ctrl+N` | New conversation |
| `Esc` | Close suggestions dropdown |
| `↑` (in input) | Edit last message |
