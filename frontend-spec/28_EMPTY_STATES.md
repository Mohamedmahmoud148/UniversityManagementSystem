# 28 — Empty States

Every list, table, and collection must have an empty state. Empty states should be helpful, not just "No data."

## Pattern
```tsx
function EmptyState({ icon: Icon, title, description, action }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4 text-center">
      <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mb-4">
        <Icon className="w-8 h-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-semibold mb-2">{title}</h3>
      <p className="text-muted-foreground max-w-sm mb-6">{description}</p>
      {action && (
        <Button onClick={action.onClick}>{action.label}</Button>
      )}
    </div>
  );
}
```

## Empty State Catalog

| Screen | Icon | Title | Description | Action |
|--------|------|-------|-------------|--------|
| Student exams list (no upcoming) | `Calendar` | No upcoming exams | You don't have any scheduled exams right now | — |
| Student assignments (all submitted) | `CheckCircle` | All caught up! | You've submitted all your assignments | — |
| Student grades (none yet) | `Star` | No grades yet | Your grades will appear here after your first exam | — |
| Flashcard decks (none) | `BookOpen` | No flashcard decks | Create your first deck to start studying | Generate Deck |
| Chat conversations (none) | `MessageCircle` | Start a conversation | Ask the AI anything about your courses | New Chat |
| Notifications (none) | `Bell` | All caught up! | You have no new notifications | — |
| Doctor exam results (0 submitted) | `FileText` | No submissions yet | Students haven't submitted yet | — |
| Teaching intelligence (no offerings) | `BarChart3` | No course offerings | You don't have any active course offerings this semester | — |
| Student insights (none) | `Sparkles` | No insights yet | Your AI insights will appear as you use the platform | Start Session |
| Admin students (0) | `Users` | No students found | No students match your search | Import Students |
| Complaint list (none) | `MessageSquare` | No complaints | Everything is running smoothly! | — |
| Materials (none uploaded) | `FileUp` | No materials | Upload the first material for this course | Upload |
| Search results (0) | `Search` | No results | Try different search terms | — |
