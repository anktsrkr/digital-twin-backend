---
title: "AI-Native Frontend Architecture: Vite, React, AG-UI Streaming & Generative UI"
category: "Architecture"
company: "Enterprise AI Engineering & Open Source"
role: "Lead Full-Stack & AI Architect"
startDate: "2024-01"
endDate: "Present"
sourceName: "Architecture: AI-Native Frontend"
sourceLink: "#architecture-frontend-agui"
technologies:
  - "React 19"
  - "Vite"
  - "TypeScript"
  - "CopilotKit"
  - "AG-UI Protocol"
  - "Server-Sent Events (SSE)"
  - "Generative UI"
  - "react-markdown"
  - "remark-gfm"
  - "Clerk React"
  - "Lucide Icons"
---

# AI-Native Frontend Architecture: React, AG-UI & Generative UI

The frontend of the Digital Twin Resume Assistant is an AI-native single-page application built with **React 19**, **Vite**, **TypeScript**, and **CopilotKit / AG-UI Client**. It is designed with high visual fidelity, modern typography, glassmorphism aesthetics, and real-time streaming ergonomics tailored for hiring managers and recruiters.

## 1. AG-UI Server-Sent Events (SSE) Streaming
- Directly consumes the backend's `POST /agentic_chat` AG-UI stream using Server-Sent Events.
- Incremental token streaming ensures low First-Token Latency (TTFT) and smooth typewriter rendering.
- Handles multi-turn conversational state, error recovery, and auto-scroll behaviors seamlessly.

## 2. Interactive Citation Drawer (`CitationDrawer.tsx`)
- As the agent generates answers grounded in Ankit's verified background, citations are embedded as interactive badges (e.g. `[1]`, `[Work Experience: Tier-1 UK Grocery Retailer]`).
- Clicking any citation opens a smooth, right-side sliding drawer displaying the full verified source text, company, date range, and related technologies.
- Eliminates AI hallucination skepticism by giving recruiters direct proof of every stated achievement.

## 3. Generative UI Action Cards (`ActionCards.tsx`) & Live Slot Picker (`LiveSlotPicker.tsx`)
- When the conversation triggers specific actions, the UI transitions beyond plain text into interactive UI widgets:
  - **Live Interview Slot Picker:** Multi-step interactive calendar allowing recruiters to browse open slots, select meeting durations (15m, 30m, 60m), and book meetings directly into Cal.com without leaving the page.
  - **Download Resume Card:** Action widget to view or download official PDF resumes, LinkedIn profiles, and GitHub repositories.
  - **Architecture Dossier Viewer (`ArchitectureDossier.tsx`):** In-depth visual modal presenting interactive system architecture diagrams and deep-dive technical briefs.

## 4. Contextual Follow-up Chips (`FollowUpPills.tsx`)
- Automatically renders dynamic, AI-generated suggestion pills after each message turn (e.g. *"Tell me about Tier-1 grocery peak trading"*, *"How did you design the Stub Identity Platform?"*, *"What are your salary & visa requirements?"*).
- Guides recruiters toward the candidate's core strengths and technical milestones with single-click navigation.

## 5. Recruiter Security & Gatekeeper
- **Clerk Authentication Integration:** Seamless magic link and social login for verified recruiters.
- **`BlockedEmailModal.tsx`:** Intercepts attempts from temporary/disposable email services (e.g. Mailinator, GuerrillaMail) and prompts users for authentic corporate or personal credentials.
