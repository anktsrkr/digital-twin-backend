import React, { useState, useEffect } from 'react';
import { X, Mail, ShieldAlert, CheckCircle2, Building, ArrowRight, Lock, ExternalLink, KeyRound, ArrowLeft, RefreshCw } from 'lucide-react';
import confetti from 'canvas-confetti';
import { validateRecruiterEmail, type EmailCheckResult } from '../lib/emailValidation';
import { supabase, isSupabaseConfigured } from '../lib/supabaseClient';

interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (email: string, company?: string) => void;
}

export const AuthModal: React.FC<AuthModalProps> = ({ isOpen, onClose, onSuccess }) => {
  const [email, setEmail] = useState('');
  const [otpCode, setOtpCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [sentMagicLink, setSentMagicLink] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [emailCheck, setEmailCheck] = useState<EmailCheckResult | null>(null);

  // Reset modal state whenever opened
  useEffect(() => {
    if (isOpen) {
      setSentMagicLink(false);
      setOtpCode('');
      setErrorMsg(null);
      setLoading(false);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleEmailChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setEmail(val);
    setErrorMsg(null);

    if (val.includes('@') && val.includes('.')) {
      const check = validateRecruiterEmail(val);
      setEmailCheck(check);
      if (check.isDisposable) {
        setErrorMsg(check.message || 'Disposable email addresses are not accepted.');
      }
    } else {
      setEmailCheck(null);
    }
  };

  const handleSendMagicLink = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const check = validateRecruiterEmail(email);

    if (!check.isValid || check.isDisposable) {
      setErrorMsg(check.message || 'Please provide a valid corporate or standard email address.');
      return;
    }

    setLoading(true);
    setErrorMsg(null);

    try {
      if (isSupabaseConfigured) {
        const { error } = await supabase.auth.signInWithOtp({
          email: email.trim(),
          options: {
            emailRedirectTo: window.location.origin
          }
        });

        if (error) {
          throw error;
        }

        setSentMagicLink(true);
        confetti({ particleCount: 60, spread: 50, origin: { y: 0.6 } });
      } else {
        // Fallback Mock Mode
        setSentMagicLink(true);
        confetti({ particleCount: 80, spread: 70, origin: { y: 0.6 } });
        setTimeout(() => {
          onSuccess(email.trim(), check.inferredCompany);
          onClose();
        }, 1200);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to dispatch magic link. Please check your email and try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!otpCode.trim()) return;

    setLoading(true);
    setErrorMsg(null);

    try {
      // 1. Attempt verification with type 'email'
      let { data, error } = await supabase.auth.verifyOtp({
        email: email.trim(),
        token: otpCode.trim(),
        type: 'email'
      });

      // 2. Fallback to type 'signup' for first-time signups
      if (error) {
        const signupAttempt = await supabase.auth.verifyOtp({
          email: email.trim(),
          token: otpCode.trim(),
          type: 'signup'
        });
        if (!signupAttempt.error) {
          data = signupAttempt.data;
          error = null;
        }
      }

      if (error) throw error;

      confetti({ particleCount: 100, spread: 70, origin: { y: 0.6 } });
      onSuccess(data.user?.email || email.trim(), emailCheck?.inferredCompany);
      onClose();
    } catch (err: any) {
      setErrorMsg(err.message || 'Token has expired or is invalid. Please request a new code.');
    } finally {
      setLoading(false);
    }
  };

  const isLocalEnv = typeof window !== 'undefined' && (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');

  return (
    <div style={{
      position: 'fixed',
      inset: 0,
      background: 'rgba(15, 23, 42, 0.45)',
      backdropFilter: 'blur(6px)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 9999,
      padding: '1rem'
    }}>
      <div style={{
        width: '100%',
        maxWidth: '450px',
        padding: '1.85rem',
        background: '#FFFFFF',
        position: 'relative',
        boxShadow: 'var(--shadow-xl)',
        borderRadius: 'var(--radius-xl)',
        border: '1px solid var(--border-hairline)'
      }}>
        {/* Close Button */}
        <button 
          onClick={onClose}
          style={{
            position: 'absolute',
            top: '1.25rem',
            right: '1.25rem',
            background: 'var(--bg-surface-subtle)',
            border: '1px solid var(--border-hairline)',
            borderRadius: 'var(--radius-sm)',
            width: '28px',
            height: '28px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: 'pointer',
            color: 'var(--text-secondary)'
          }}
        >
          <X size={14} />
        </button>

        {/* Modal Header */}
        <div style={{ textAlign: 'center', marginBottom: '1.35rem' }}>
          <div style={{
            width: '42px',
            height: '42px',
            borderRadius: '10px',
            background: 'var(--accent-slate)',
            color: '#FFFFFF',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 0.65rem',
            boxShadow: '0 1px 3px rgba(0, 0, 0, 0.12)'
          }}>
            <KeyRound size={20} />
          </div>
          <h3 style={{ fontSize: '1.15rem', fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>
            Recruiter & Screening Verification
          </h3>
          <p style={{ fontSize: '0.82rem', color: 'var(--text-muted)', marginTop: '0.2rem' }}>
            Instant passwordless access via Supabase Auth.
          </p>
        </div>

        {sentMagicLink ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.15rem' }}>
            <div style={{
              textAlign: 'center',
              padding: '1.1rem 1rem',
              background: 'var(--accent-emerald-subtle)',
              borderRadius: 'var(--radius-md)',
              border: '1px solid var(--accent-emerald-border)'
            }}>
              <CheckCircle2 size={32} color="var(--accent-emerald)" style={{ margin: '0 auto 0.4rem' }} />
              <h4 style={{ fontSize: '0.9rem', fontWeight: 700, color: '#065F46' }}>
                Verification Token Dispatched
              </h4>
              <p style={{ fontSize: '0.78125rem', color: '#047857', marginTop: '0.2rem' }}>
                We sent a secure sign-in token to <strong>{email}</strong>.
              </p>
            </div>

            {/* Local Inbucket Quick Link */}
            {isLocalEnv && (
              <a
                href="http://localhost:9000"
                target="_blank"
                rel="noreferrer"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '0.45rem',
                  padding: '0.65rem',
                  background: 'var(--bg-surface-subtle)',
                  border: '1px solid var(--border-hairline)',
                  borderRadius: 'var(--radius-md)',
                  color: 'var(--text-primary)',
                  textDecoration: 'none',
                  fontSize: '0.82rem',
                  fontWeight: 600
                }}
              >
                <span>Open Inbucket Local Inbox</span>
                <ExternalLink size={14} />
              </a>
            )}

            {/* 6-digit OTP Token Form */}
            <form onSubmit={handleVerifyOtp} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.78125rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '0.35rem' }}>
                  Or Enter 6-Digit OTP / Token:
                </label>
                <div style={{ position: 'relative' }}>
                  <KeyRound size={16} color="var(--text-muted)" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
                  <input
                    type="text"
                    placeholder="e.g. 123456"
                    value={otpCode}
                    onChange={(e) => {
                      setOtpCode(e.target.value);
                      setErrorMsg(null);
                    }}
                    style={{
                      width: '100%',
                      padding: '0.65rem 0.75rem 0.65rem 2.2rem',
                      borderRadius: 'var(--radius-md)',
                      border: errorMsg ? '1px solid #EF4444' : '1px solid var(--border-hairline)',
                      fontSize: '0.9rem',
                      outline: 'none',
                      fontFamily: 'var(--font-mono)',
                      letterSpacing: '2px',
                      background: 'var(--bg-surface-subtle)'
                    }}
                    onFocus={(e) => (e.currentTarget.style.borderColor = 'var(--accent-slate)')}
                    onBlur={(e) => (e.currentTarget.style.borderColor = 'var(--border-hairline)')}
                  />
                </div>
              </div>

              {errorMsg && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.72rem', color: '#DC2626', fontWeight: 600 }}>
                  <ShieldAlert size={13} />
                  <span>{errorMsg}</span>
                </div>
              )}

              <button
                type="submit"
                className="btn-primary"
                disabled={loading || !otpCode.trim()}
                style={{ width: '100%', padding: '0.65rem' }}
              >
                {loading ? 'Verifying...' : 'Verify Code & Sign In'}
              </button>
            </form>

            {/* Navigation options: Back / Resend */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '0.2rem', fontSize: '0.78125rem' }}>
              <button
                type="button"
                onClick={() => {
                  setSentMagicLink(false);
                  setOtpCode('');
                  setErrorMsg(null);
                }}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-muted)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.3rem',
                  padding: 0,
                  fontSize: '0.78125rem',
                  fontWeight: 500
                }}
              >
                <ArrowLeft size={13} />
                <span>Use different email</span>
              </button>

              <button
                type="button"
                onClick={() => handleSendMagicLink()}
                disabled={loading}
                style={{
                  background: 'none',
                  border: 'none',
                  color: 'var(--text-primary)',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '0.3rem',
                  padding: 0,
                  fontSize: '0.78125rem',
                  fontWeight: 600
                }}
              >
                <RefreshCw size={12} />
                <span>Resend Code</span>
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSendMagicLink} style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.78125rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '0.35rem' }}>
                Your Corporate or Standard Email
              </label>
              <div style={{ position: 'relative' }}>
                <Mail size={16} color="var(--text-muted)" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
                <input 
                  type="email"
                  required
                  placeholder="e.g. sarah.recruiter@stripe.com"
                  value={email}
                  onChange={handleEmailChange}
                  style={{
                    width: '100%',
                    padding: '0.65rem 0.75rem 0.65rem 2.2rem',
                    borderRadius: 'var(--radius-md)',
                    border: emailCheck?.isDisposable ? '1px solid #EF4444' : '1px solid var(--border-hairline)',
                    fontSize: '0.88rem',
                    outline: 'none',
                    fontFamily: 'inherit',
                    transition: 'all 0.15s ease',
                    background: 'var(--bg-surface-subtle)'
                  }}
                  onFocus={(e) => (e.currentTarget.style.borderColor = 'var(--accent-slate)')}
                  onBlur={(e) => (e.currentTarget.style.borderColor = 'var(--border-hairline)')}
                />
              </div>

              {/* Inferred Company or Disposable Warning */}
              {emailCheck?.inferredCompany && !emailCheck.isDisposable && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', marginTop: '0.35rem', fontSize: '0.72rem', color: 'var(--accent-slate)', fontWeight: 600 }}>
                  <Building size={12} />
                  <span>Identified as Recruiter from {emailCheck.inferredCompany}</span>
                </div>
              )}

              {errorMsg && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', marginTop: '0.35rem', fontSize: '0.72rem', color: '#DC2626', fontWeight: 600 }}>
                  <ShieldAlert size={13} />
                  <span>{errorMsg}</span>
                </div>
              )}
            </div>

            <div style={{
              background: 'var(--bg-surface-subtle)',
              border: '1px solid var(--border-hairline)',
              borderRadius: 'var(--radius-sm)',
              padding: '0.55rem 0.7rem',
              display: 'flex',
              alignItems: 'center',
              gap: '0.45rem',
              fontSize: '0.72rem',
              color: 'var(--text-muted)'
            }}>
              <Lock size={13} color="var(--text-secondary)" />
              <span>Temporary / disposable email domains are blocked to prevent spam.</span>
            </div>

            <button 
              type="submit" 
              className="btn-primary" 
              disabled={loading || emailCheck?.isDisposable}
              style={{ width: '100%', marginTop: '0.35rem', padding: '0.65rem' }}
            >
              {loading ? 'Validating & Sending...' : (
                <>
                  <span>Send Magic Link</span>
                  <ArrowRight size={14} />
                </>
              )}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
