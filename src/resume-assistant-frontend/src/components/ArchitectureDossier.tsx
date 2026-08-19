import React, { useState } from 'react';
import { 
  Zap, 
  Bot, 
  Building2, 
  Calendar, 
  Download, 
  ChevronRight,
  Globe,
  ShieldCheck,
  KeyRound
} from 'lucide-react';

export interface ArchitectureDossierProps {
  onSelectPrompt: (prompt: string) => void;
  onScheduleClick: () => void;
  onDownloadPdf: () => void;
  isAgentRunning?: boolean;
  isAuthenticated?: boolean;
  recruiterEmail?: string;
  recruiterCompany?: string;
  onOpenAuth?: () => void;
  onSignOut?: () => void;
}

interface CaseStudy {
  id: string;
  category: string;
  title: string;
  metrics: string;
  description: string;
  technologies: string[];
  prompt: string;
  icon: React.ReactNode;
}

const CASE_STUDIES: CaseStudy[] = [
  {
    id: 'asda-scale',
    category: 'FLAGSHIP HIGH-SCALE',
    title: "ASDA eCommerce Picking Platform",
    metrics: "700k+ orders/wk • 90k/30-min peak • 0 incidents",
    description: "Architected decoupled event-driven platform handling flash-sale trading surges with Azure Service Bus Premium sessions & Redis state.",
    technologies: ['.NET 8', 'Azure Service Bus', 'Redis', 'Cosmos DB', 'Event Grid'],
    prompt: "How did you achieve zero downtime during ASDA's 90k/30-min peak trading?",
    icon: <Zap size={16} color="#D97706" />
  },
  {
    id: 'agentic-ai',
    category: 'AGENTIC AI & SECURITY',
    title: "Enterprise Multi-Agent & MCP",
    metrics: "Azure AI Foundry • SpiceDB ReBAC • pgvector",
    description: "Multi-agent systems with Microsoft Agent Framework, fine-grained authorization via SpiceDB ReBAC, and hybrid semantic search.",
    technologies: ['Agent Framework', 'MCP', 'SpiceDB', 'pgvector', 'Voyage AI'],
    prompt: "How do you secure MCP tool calling and multi-agent workflows in production?",
    icon: <Bot size={16} color="#2563EB" />
  },
  {
    id: 'enterprise-modernisation',
    category: 'ENTERPRISE MODERNISATION',
    title: "Boots UK & Belgian Railways (NMBS)",
    metrics: "25k store users • Stub Identity • Cloud Migration",
    description: "Modernised legacy on-prem infrastructure to cloud-native microservices with zero disruption to retail pharmacy and national transport.",
    technologies: ['Azure Functions', 'API Management', 'Identity Platform', 'Event Hubs'],
    prompt: "What was your architecture strategy for the Boots UK and Belgian Railways modernisations?",
    icon: <Building2 size={16} color="#059669" />
  }
];

const TECH_MATRIX = [
  { group: 'Cloud & Systems', items: ['Microsoft Azure', 'Event-Driven', 'Microservices', 'Kubernetes', 'Serverless'] },
  { group: 'AI & Data', items: ['Agentic AI', 'MCP Protocol', 'Enterprise RAG', 'SpiceDB ReBAC', 'pgvector', 'Voyage AI'] },
  { group: 'Engineering', items: ['.NET 8 / C#', 'Azure Service Bus', 'Redis', 'Cosmos DB', 'PostgreSQL', 'TypeScript'] }
];

export const ArchitectureDossier: React.FC<ArchitectureDossierProps> = ({
  onSelectPrompt,
  onScheduleClick,
  onDownloadPdf,
  isAgentRunning = false,
  isAuthenticated = false,
  recruiterEmail,
  recruiterCompany,
  onOpenAuth,
  onSignOut
}) => {
  const [activeTab, setActiveTab] = useState<'case-studies' | 'tech-matrix'>('case-studies');

  return (
    <aside style={{
      display: 'flex',
      flexDirection: 'column',
      gap: '0.85rem',
      width: '100%',
      height: '100%',
      overflowY: 'auto',
      paddingRight: '0.25rem'
    }}>
      {/* 1. Candidate Executive Overview Card */}
      <div style={{
        background: '#FFFFFF',
        border: '1px solid var(--border-hairline)',
        borderRadius: 'var(--radius-lg)',
        padding: '1.2rem',
        boxShadow: 'var(--shadow-xs)'
      }}>
        {/* Candidate Identity & Experience Badge */}
        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '0.35rem' }}>
          <div>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.025em', lineHeight: 1.2 }}>
              Ankit Sarkar
            </h3>
            <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
              AI Solutions Architect & Principal
            </span>
          </div>
          <span className="badge-mono">13+ Yrs Exp</span>
        </div>

        <p style={{ fontSize: '0.78125rem', color: 'var(--text-secondary)', marginTop: '0.2rem', lineHeight: 1.45 }}>
          Architecting high-scale cloud platforms and enterprise agentic systems on Microsoft Azure.
        </p>

        {/* Quick Visa & Location Status */}
        <div style={{
          marginTop: '0.7rem',
          padding: '0.6rem 0.75rem',
          background: 'var(--bg-surface-subtle)',
          borderRadius: 'var(--radius-md)',
          border: '1px solid var(--border-hairline)',
          display: 'flex',
          flexDirection: 'column',
          gap: '0.35rem'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '0.73rem' }}>
            <span style={{ color: 'var(--text-muted)' }}>Visa Status:</span>
            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>UK Skilled Worker (Confirmed)</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '0.73rem' }}>
            <span style={{ color: 'var(--text-muted)' }}>Location Pref:</span>
            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>London / Hybrid / Remote</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '0.73rem' }}>
            <span style={{ color: 'var(--text-muted)' }}>Availability:</span>
            <span style={{ fontWeight: 600, color: 'var(--accent-emerald)' }}>Immediate / 1 Month Notice</span>
          </div>
        </div>

        {/* Social Links & Recruiter Auth Status Row */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginTop: '0.75rem',
          paddingTop: '0.65rem',
          borderTop: '1px solid var(--border-hairline)',
          flexWrap: 'wrap',
          gap: '0.4rem'
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
            <a href="https://anktsrkr.github.io" target="_blank" rel="noreferrer" className="btn-icon" title="Technical Blog">
              <Globe size={13} color="var(--text-secondary)" />
            </a>
            <a href="https://github.com/anktsrkr" target="_blank" rel="noreferrer" className="btn-icon" title="GitHub">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.403 5.403 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4" />
                <path d="M9 18c-4.51 2-5-2-7-2" />
              </svg>
            </a>
            <a href="https://linkedin.com/in/sarkaran" target="_blank" rel="noreferrer" className="btn-icon" title="LinkedIn">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#0A66C2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z" />
                <rect x="2" y="9" width="4" height="12" />
                <circle cx="4" cy="4" r="2" />
              </svg>
            </a>
          </div>

          {isAuthenticated ? (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.3rem',
              background: 'var(--accent-emerald-subtle)',
              border: '1px solid var(--accent-emerald-border)',
              padding: '0.2rem 0.5rem',
              borderRadius: 'var(--radius-sm)',
              fontSize: '0.72rem'
            }}>
              <ShieldCheck size={12} color="var(--accent-emerald)" />
              <span style={{ fontWeight: 600, color: '#065F46' }}>
                {recruiterCompany ? `${recruiterCompany}` : (recruiterEmail?.split('@')[0] || 'Recruiter')}
              </span>
              <button 
                onClick={onSignOut}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                  fontSize: '0.68rem',
                  textDecoration: 'underline',
                  marginLeft: '0.15rem'
                }}
              >
                Sign Out
              </button>
            </div>
          ) : (
            <button 
              onClick={onOpenAuth} 
              className="btn-secondary" 
              style={{ padding: '0.25rem 0.55rem', fontSize: '0.72rem', gap: '0.3rem' }}
            >
              <KeyRound size={12} />
              <span>Recruiter Access</span>
            </button>
          )}
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', gap: '0.45rem', marginTop: '0.75rem' }}>
          <button 
            onClick={onScheduleClick} 
            className="btn-primary" 
            style={{ flex: 1, padding: '0.5rem 0.75rem', fontSize: '0.78125rem' }}
          >
            <Calendar size={13} />
            <span>Book Screening</span>
          </button>

          <button 
            onClick={onDownloadPdf} 
            className="btn-secondary" 
            style={{ flex: 1, padding: '0.5rem 0.75rem', fontSize: '0.78125rem' }}
          >
            <Download size={13} />
            <span>PDF Resume</span>
          </button>
        </div>
      </div>

      {/* 2. Sub-navigation tabs */}
      <div style={{
        display: 'flex',
        background: 'var(--bg-surface-subtle)',
        padding: '0.2rem',
        borderRadius: 'var(--radius-md)',
        border: '1px solid var(--border-hairline)'
      }}>
        <button
          onClick={() => setActiveTab('case-studies')}
          style={{
            flex: 1,
            padding: '0.4rem',
            border: 'none',
            borderRadius: 'var(--radius-sm)',
            background: activeTab === 'case-studies' ? '#FFFFFF' : 'transparent',
            color: activeTab === 'case-studies' ? 'var(--text-primary)' : 'var(--text-muted)',
            fontWeight: activeTab === 'case-studies' ? 700 : 500,
            fontSize: '0.75rem',
            cursor: 'pointer',
            boxShadow: activeTab === 'case-studies' ? 'var(--shadow-xs)' : 'none',
            transition: 'all 0.12s ease'
          }}
        >
          Flagship Architectures
        </button>
        <button
          onClick={() => setActiveTab('tech-matrix')}
          style={{
            flex: 1,
            padding: '0.4rem',
            border: 'none',
            borderRadius: 'var(--radius-sm)',
            background: activeTab === 'tech-matrix' ? '#FFFFFF' : 'transparent',
            color: activeTab === 'tech-matrix' ? 'var(--text-primary)' : 'var(--text-muted)',
            fontWeight: activeTab === 'tech-matrix' ? 700 : 500,
            fontSize: '0.75rem',
            cursor: 'pointer',
            boxShadow: activeTab === 'tech-matrix' ? 'var(--shadow-xs)' : 'none',
            transition: 'all 0.12s ease'
          }}
        >
          Competency Matrix
        </button>
      </div>

      {/* 3. Tab Content */}
      {activeTab === 'case-studies' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>
          {CASE_STUDIES.map((study) => (
            <div
              key={study.id}
              onClick={() => {
                if (!isAgentRunning) onSelectPrompt(study.prompt);
              }}
              style={{
                background: '#FFFFFF',
                border: '1px solid var(--border-hairline)',
                borderRadius: 'var(--radius-md)',
                padding: '0.95rem 1.05rem',
                cursor: isAgentRunning ? 'not-allowed' : 'pointer',
                transition: 'all 0.15s cubic-bezier(0.16, 1, 0.3, 1)',
                boxShadow: 'var(--shadow-xs)',
                position: 'relative'
              }}
              onMouseEnter={(e) => {
                if (isAgentRunning) return;
                e.currentTarget.style.borderColor = 'var(--accent-slate)';
                e.currentTarget.style.transform = 'translateY(-1.5px)';
                e.currentTarget.style.boxShadow = 'var(--shadow-sm)';
              }}
              onMouseLeave={(e) => {
                if (isAgentRunning) return;
                e.currentTarget.style.borderColor = 'var(--border-hairline)';
                e.currentTarget.style.transform = 'none';
                e.currentTarget.style.boxShadow = 'var(--shadow-xs)';
              }}
              title={`Click to ask Digital Twin: "${study.prompt}"`}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.35rem' }}>
                <span style={{ fontSize: '0.65rem', fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '0.05em' }}>
                  {study.category}
                </span>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.2rem', color: 'var(--text-muted)', fontSize: '0.6875rem' }}>
                  <span>Ask Twin</span>
                  <ChevronRight size={12} />
                </div>
              </div>

              <div style={{ display: 'flex', alignItems: 'center', gap: '0.45rem', marginBottom: '0.25rem' }}>
                {study.icon}
                <h4 style={{ fontSize: '0.88rem', fontWeight: 700, color: 'var(--text-primary)', lineHeight: 1.25 }}>
                  {study.title}
                </h4>
              </div>

              <div style={{
                fontFamily: 'var(--font-mono)',
                fontSize: '0.6875rem',
                color: 'var(--accent-slate)',
                fontWeight: 600,
                marginBottom: '0.4rem'
              }}>
                ⚡ {study.metrics}
              </div>

              <p style={{ fontSize: '0.76rem', color: 'var(--text-secondary)', lineHeight: 1.45, marginBottom: '0.55rem' }}>
                {study.description}
              </p>

              <div style={{ display: 'flex', gap: '0.3rem', flexWrap: 'wrap' }}>
                {study.technologies.map((tech, i) => (
                  <span key={i} className="badge-mono" style={{ fontSize: '0.64rem', padding: '0.1rem 0.35rem' }}>
                    {tech}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'tech-matrix' && (
        <div style={{
          background: '#FFFFFF',
          border: '1px solid var(--border-hairline)',
          borderRadius: 'var(--radius-lg)',
          padding: '1.1rem',
          display: 'flex',
          flexDirection: 'column',
          gap: '0.85rem',
          boxShadow: 'var(--shadow-xs)'
        }}>
          {TECH_MATRIX.map((group, idx) => (
            <div key={idx}>
              <div style={{ fontSize: '0.7rem', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: '0.4rem' }}>
                {group.group}
              </div>
              <div style={{ display: 'flex', gap: '0.35rem', flexWrap: 'wrap' }}>
                {group.items.map((item, i) => (
                  <button
                    key={i}
                    onClick={() => {
                      if (!isAgentRunning) onSelectPrompt(`What is Ankit's production experience with ${item}?`);
                    }}
                    disabled={isAgentRunning}
                    style={{
                      background: 'var(--bg-surface-subtle)',
                      border: '1px solid var(--border-hairline)',
                      borderRadius: 'var(--radius-sm)',
                      padding: '0.25rem 0.55rem',
                      fontFamily: 'var(--font-mono)',
                      fontSize: '0.72rem',
                      fontWeight: 500,
                      color: 'var(--text-primary)',
                      cursor: isAgentRunning ? 'not-allowed' : 'pointer',
                      transition: 'all 0.12s ease'
                    }}
                    onMouseEnter={(e) => {
                      if (isAgentRunning) return;
                      e.currentTarget.style.borderColor = 'var(--accent-slate)';
                      e.currentTarget.style.background = '#FFFFFF';
                    }}
                    onMouseLeave={(e) => {
                      if (isAgentRunning) return;
                      e.currentTarget.style.borderColor = 'var(--border-hairline)';
                      e.currentTarget.style.background = 'var(--bg-surface-subtle)';
                    }}
                    title={`Click to ask about experience with ${item}`}
                  >
                    {item}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* 4. Quick Screening Booking Mini-Widget */}
      <div style={{
        marginTop: 'auto',
        background: 'var(--bg-surface-muted)',
        border: '1px solid var(--border-hairline)',
        borderRadius: 'var(--radius-md)',
        padding: '0.85rem 1rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '0.5rem'
      }}>
        <div>
          <div style={{ fontSize: '0.78125rem', fontWeight: 700, color: 'var(--text-primary)' }}>
            Schedule Technical Screen
          </div>
          <div style={{ fontSize: '0.7rem', color: 'var(--text-muted)', marginTop: '0.1rem' }}>
            Direct sync with Cal.com (London / GMT)
          </div>
        </div>
        <button
          onClick={onScheduleClick}
          className="btn-secondary"
          style={{ padding: '0.35rem 0.65rem', fontSize: '0.72rem', whiteSpace: 'nowrap' }}
        >
          <Calendar size={12} />
          <span>Book via Twin</span>
        </button>
      </div>
    </aside>
  );
};
