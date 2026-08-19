import { useState, useEffect, useMemo, useCallback } from 'react';
import { CopilotKit } from '@copilotkit/react-core';
import { HeroHeader } from './components/HeroHeader';
import { ArchitectureDossier } from './components/ArchitectureDossier';
import { DigitalTwinChat } from './components/DigitalTwinChat';
import { AuthModal } from './components/AuthModal';
import { CitationDrawer, type CitationDetail } from './components/CitationDrawer';
import { supabase } from './lib/supabaseClient';
import { BookOpen, Terminal } from 'lucide-react';
import './styles/index.css';

export function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [recruiterEmail, setRecruiterEmail] = useState<string | undefined>(undefined);
  const [recruiterCompany, setRecruiterCompany] = useState<string | undefined>(undefined);
  const [isAuthOpen, setIsAuthOpen] = useState(false);
  const [selectedCitation, setSelectedCitation] = useState<CitationDetail | null>(null);
  const [selectedPrompt, setSelectedPrompt] = useState<string | null>(null);
  const [isAgentRunning, setIsAgentRunning] = useState(false);
  const [mobileTab, setMobileTab] = useState<'dossier' | 'terminal'>('dossier');

  const backendUrl = typeof window !== 'undefined' && window.location.hostname === 'localhost'
    ? 'http://localhost:5000'
    : (import.meta.env.VITE_BACKEND_API_URL || import.meta.env.VITE_API_URL || 'http://localhost:5000');

  useEffect(() => {
    // Check saved session in localStorage
    const savedEmail = localStorage.getItem('recruiter_email');
    const savedCompany = localStorage.getItem('recruiter_company');
    if (savedEmail) {
      setIsAuthenticated(true);
      setRecruiterEmail(savedEmail);
      if (savedCompany) setRecruiterCompany(savedCompany);
    }

    // Listen to Supabase Auth State Changes
    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session?.user?.email) {
        setIsAuthenticated(true);
        setRecruiterEmail(session.user.email);
        localStorage.setItem('recruiter_email', session.user.email);
      }
    });

    return () => {
      subscription.unsubscribe();
    };
  }, []);

  const handleAuthSuccess = (email: string, company?: string) => {
    setIsAuthenticated(true);
    setRecruiterEmail(email);
    if (company) setRecruiterCompany(company);
    localStorage.setItem('recruiter_email', email);
    if (company) localStorage.setItem('recruiter_company', company);
  };

  const handleSignOut = async () => {
    try {
      await supabase.auth.signOut();
    } catch { }
    setIsAuthenticated(false);
    setRecruiterEmail(undefined);
    setRecruiterCompany(undefined);
    localStorage.removeItem('recruiter_email');
    localStorage.removeItem('recruiter_company');
  };

  const handleDownloadPdf = useCallback(() => {
    const link = document.createElement('a');
    link.href = '/resume.pdf';
    link.download = 'Ankit_Sarkar_AI_Solutions_Architect_Resume.pdf';
    link.click();
  }, []);

  const handleSelectPrompt = (prompt: string) => {
    setSelectedPrompt(prompt);
    // On mobile, auto-switch to terminal tab to see the live response
    setMobileTab('terminal');
  };

  const copilotHeaders = useMemo(() => ({
    ...(recruiterEmail ? { 'X-Recruiter-Email': recruiterEmail } : {}),
    ...(recruiterCompany ? { 'X-Recruiter-Company': recruiterCompany } : {})
  }), [recruiterEmail, recruiterCompany]);

  return (
    <CopilotKit
      runtimeUrl={`${backendUrl}/agentic_chat`}
      agent="agentic_chat"
      showDevConsole={false}
      headers={copilotHeaders}
    >
      <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
        <HeroHeader
          isAuthenticated={isAuthenticated}
          recruiterEmail={recruiterEmail}
          recruiterCompany={recruiterCompany}
          onOpenAuth={() => setIsAuthOpen(true)}
          onSignOut={handleSignOut}
          onScheduleClick={() => window.open('https://cal.com/anktsrkr', '_blank')}
          onDownloadPdf={handleDownloadPdf}
        />

        <div className="console-container">
          {/* Mobile Segmented View Switcher */}
          <div className="mobile-pane-switcher">
            <button
              className={`mobile-tab-btn ${mobileTab === 'dossier' ? 'active' : ''}`}
              onClick={() => setMobileTab('dossier')}
            >
              <BookOpen size={14} />
              <span>Architecture Dossier</span>
            </button>
            <button
              className={`mobile-tab-btn ${mobileTab === 'terminal' ? 'active' : ''}`}
              onClick={() => setMobileTab('terminal')}
            >
              <Terminal size={14} />
              <span>Digital Twin Terminal {isAgentRunning ? '⚡' : ''}</span>
            </button>
          </div>

          {/* Main Dual-Pane Console Grid */}
          <main className="console-main-grid">
            {/* Left Pane: Architecture Dossier */}
            <section className={`dossier-pane ${mobileTab !== 'dossier' ? 'hide-mobile' : ''}`}>
              <ArchitectureDossier
                onSelectPrompt={handleSelectPrompt}
                onScheduleClick={() => window.open('https://cal.com/anktsrkr', '_blank')}
                onDownloadPdf={handleDownloadPdf}
                isAgentRunning={isAgentRunning}
              />
            </section>

            {/* Right Pane: Digital Twin Interactive Console */}
            <section className={`terminal-pane ${mobileTab !== 'terminal' ? 'hide-mobile' : ''}`}>
              <DigitalTwinChat
                isAuthenticated={isAuthenticated}
                recruiterEmail={recruiterEmail}
                onOpenAuth={() => setIsAuthOpen(true)}
                onOpenCitation={(citation) => setSelectedCitation(citation)}
                externalPrompt={selectedPrompt}
                onClearExternalPrompt={() => setSelectedPrompt(null)}
                onAgentStateChange={setIsAgentRunning}
              />
            </section>
          </main>
        </div>

        {/* Recruiter Magic Link Modal */}
        <AuthModal
          isOpen={isAuthOpen}
          onClose={() => setIsAuthOpen(false)}
          onSuccess={handleAuthSuccess}
        />

        {/* Interactive Citation Drawer */}
        <CitationDrawer
          isOpen={selectedCitation !== null}
          citation={selectedCitation}
          onClose={() => setSelectedCitation(null)}
        />
      </div>
    </CopilotKit>
  );
}

export default App;
