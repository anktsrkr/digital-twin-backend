import React from 'react';
import { Calendar, Download, ShieldCheck, Globe, KeyRound } from 'lucide-react';

interface HeroHeaderProps {
  isAuthenticated: boolean;
  recruiterEmail?: string;
  recruiterCompany?: string;
  onOpenAuth: () => void;
  onSignOut: () => void;
  onScheduleClick: () => void;
  onDownloadPdf: () => void;
}

export const HeroHeader: React.FC<HeroHeaderProps> = ({
  isAuthenticated,
  recruiterEmail,
  recruiterCompany,
  onOpenAuth,
  onSignOut,
  onScheduleClick,
  onDownloadPdf
}) => {
  return (
    <header style={{
      width: '100%',
      position: 'sticky',
      top: 0,
      zIndex: 100,
      background: 'rgba(255, 255, 255, 0.94)',
      backdropFilter: 'blur(12px)',
      WebkitBackdropFilter: 'blur(12px)',
      borderBottom: '1px solid var(--border-hairline)',
      padding: '0.7rem 1.25rem',
      transition: 'all 0.15s ease'
    }}>
      <div style={{
        maxWidth: '1080px',
        margin: '0 auto',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '0.75rem'
      }}>
        {/* Brand / Candidate Info */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <div style={{
            width: '38px',
            height: '38px',
            borderRadius: '10px',
            background: 'var(--accent-slate)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#FFFFFF',
            fontFamily: 'var(--font-sans)',
            fontWeight: 800,
            fontSize: '0.92rem',
            letterSpacing: '-0.02em',
            boxShadow: '0 1px 3px rgba(0, 0, 0, 0.15)',
            flexShrink: 0
          }}>
            AS
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
              <h1 style={{ fontSize: '1.05rem', fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em', lineHeight: 1.2 }}>
                Ankit Sarkar
              </h1>
              <span className="badge-pill badge-available" style={{ padding: '0.12rem 0.5rem', fontSize: '0.6875rem' }}>
                <span className="status-dot"></span>
                AI Solutions Architect & Principal
              </span>
            </div>
            <p style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.1rem', letterSpacing: '-0.01em' }}>
              Agentic AI • Microsoft Azure • Enterprise RAG & SpiceDB ReBAC
            </p>
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', flexWrap: 'wrap' }}>
          <button onClick={onScheduleClick} className="btn-secondary" title="Book 30-min Technical Screening">
            <Calendar size={13} color="var(--text-primary)" />
            <span>Book Call</span>
          </button>
          
          <button onClick={onDownloadPdf} className="btn-secondary" title="Download Official PDF Resume">
            <Download size={13} color="var(--text-primary)" />
            <span>PDF Resume</span>
          </button>

          <a href="https://anktsrkr.github.io" target="_blank" rel="noreferrer" className="btn-icon" title="Technical Blog">
            <Globe size={14} color="var(--text-secondary)" />
          </a>

          <a href="https://github.com/anktsrkr" target="_blank" rel="noreferrer" className="btn-icon" title="GitHub">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.403 5.403 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4" />
              <path d="M9 18c-4.51 2-5-2-7-2" />
            </svg>
          </a>

          <a href="https://linkedin.com/in/sarkaran" target="_blank" rel="noreferrer" className="btn-icon" title="LinkedIn">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#0A66C2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z" />
              <rect x="2" y="9" width="4" height="12" />
              <circle cx="4" cy="4" r="2" />
            </svg>
          </a>

          {isAuthenticated ? (
            <div style={{
              display: 'flex',
              alignItems: 'center',
              gap: '0.4rem',
              background: 'var(--accent-emerald-subtle)',
              border: '1px solid var(--accent-emerald-border)',
              padding: '0.3rem 0.65rem',
              borderRadius: 'var(--radius-md)',
              fontSize: '0.75rem'
            }}>
              <ShieldCheck size={13} color="var(--accent-emerald)" />
              <span style={{ fontWeight: 600, color: '#065F46' }}>
                {recruiterCompany ? `${recruiterCompany} Recruiter` : recruiterEmail}
              </span>
              <button 
                onClick={onSignOut}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                  fontSize: '0.7rem',
                  textDecoration: 'underline',
                  marginLeft: '0.2rem'
                }}
              >
                Sign Out
              </button>
            </div>
          ) : (
            <button onClick={onOpenAuth} className="btn-primary" style={{ padding: '0.42rem 0.8rem', fontSize: '0.78125rem' }}>
              <KeyRound size={13} />
              <span>Recruiter Access</span>
            </button>
          )}
        </div>
      </div>
    </header>
  );
};
