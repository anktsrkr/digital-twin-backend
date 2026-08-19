import React, { useState, useEffect } from 'react';
import { Calendar, Clock, ArrowUpRight, CheckCircle2, ChevronRight, Loader2, X } from 'lucide-react';

export interface SlotItem {
  date: string;
  time_utc: string;
  formatted_time: string;
  raw_time?: string;
}

export interface LiveSlotPickerProps {
  slots?: SlotItem[];
  timeZone?: string;
  duration?: number;
  bookingUrl?: string;
  isBooking?: boolean;
  onSelectSlot?: (slot: SlotItem) => void;
}

/**
 * Cleanly format slot time label without assuming a fixed UTC offset string.
 */
export const formatSlotTime = (slot: SlotItem, targetTimeZone: string): string => {
  // If formatted_time has "@ 11:30 AM (UTC+01:00)", extract the time part cleanly
  if (slot.formatted_time && slot.formatted_time.includes('@')) {
    const afterAt = slot.formatted_time.split('@')[1]?.trim() || '';
    // Strip parenthetical timezone e.g. "(UTC+01:00)", "(BST)", "(GMT)"
    const cleaned = afterAt.replace(/\s*\([^)]*\)$/, '').trim();
    if (cleaned) return cleaned;
  }

  // Fallback to Intl.DateTimeFormat parsing of raw_time or time_utc
  const raw = slot.raw_time || slot.time_utc;
  if (raw) {
    try {
      const parsed = new Date(raw);
      if (!isNaN(parsed.getTime())) {
        return parsed.toLocaleTimeString('en-GB', {
          hour: '2-digit',
          minute: '2-digit',
          hour12: true,
          timeZone: targetTimeZone
        });
      }
    } catch {
      // ignore
    }
  }

  return slot.formatted_time || slot.time_utc;
};

export const LiveSlotPicker: React.FC<LiveSlotPickerProps> = ({
  slots = [],
  timeZone = 'Europe/London',
  duration = 30,
  bookingUrl = 'https://cal.com/anktsrkr',
  isBooking = false,
  onSelectSlot
}) => {
  // Group slots by date
  const groupedSlots = React.useMemo(() => {
    const groups: Record<string, SlotItem[]> = {};
    for (const slot of slots) {
      const dateKey = slot.date || 'Upcoming';
      if (!groups[dateKey]) groups[dateKey] = [];
      groups[dateKey].push(slot);
    }
    return groups;
  }, [slots]);

  const dates = Object.keys(groupedSlots);
  const [selectedDate, setSelectedDate] = useState<string>(dates[0] || '');
  const [selectedSlot, setSelectedSlot] = useState<SlotItem | null>(null);

  // Sync selectedDate when slots change or on initial load
  useEffect(() => {
    if ((!selectedDate || !groupedSlots[selectedDate]) && dates.length > 0) {
      setSelectedDate(dates[0]);
    }
  }, [dates, selectedDate, groupedSlots]);

  const activeSlots = selectedDate ? (groupedSlots[selectedDate] || []) : [];

  const formatTabHeader = (dateStr: string) => {
    try {
      const parsed = new Date(dateStr);
      if (!isNaN(parsed.getTime())) {
        return {
          dayName: parsed.toLocaleDateString('en-GB', { weekday: 'short' }),
          dayNum: parsed.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
        };
      }
    } catch {
      // fallback
    }
    return { dayName: 'Date', dayNum: dateStr };
  };

  const handleSlotClick = (slot: SlotItem) => {
    setSelectedSlot(slot);
    if (onSelectSlot) {
      onSelectSlot(slot);
    } else {
      const directUrl = `${bookingUrl}?date=${slot.date}`;
      window.open(directUrl, '_blank');
    }
  };

  const handleClearSelection = (e: React.MouseEvent) => {
    e.stopPropagation();
    setSelectedSlot(null);
  };

  return (
    <div style={{
      margin: '0.75rem 0',
      background: '#FFFFFF',
      border: '1px solid var(--border-hairline)',
      borderRadius: 'var(--radius-lg)',
      padding: '1.15rem 1.25rem',
      boxShadow: 'var(--shadow-xs)',
      fontFamily: 'var(--font-sans)',
      maxWidth: '100%',
      overflow: 'hidden'
    }}>
      {/* Header Banner */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '0.5rem',
        paddingBottom: '0.75rem',
        borderBottom: '1px solid var(--border-hairline)',
        marginBottom: '0.85rem'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <div style={{
            width: '32px',
            height: '32px',
            borderRadius: '8px',
            background: 'var(--accent-slate)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#FFFFFF',
            boxShadow: '0 1px 3px rgba(0, 0, 0, 0.12)'
          }}>
            <Calendar size={16} />
          </div>
          <div>
            <h4 style={{ margin: 0, fontSize: '0.92rem', fontWeight: 700, color: 'var(--text-primary)', lineHeight: 1.2 }}>
              Ankit Sarkar's Live Calendar
            </h4>
            <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)', display: 'flex', alignItems: 'center', gap: '0.25rem', marginTop: '0.1rem' }}>
              <Clock size={11} />
              {duration} Min Technical Screening / Intro Call
            </span>
          </div>
        </div>

        <div style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: '0.35rem',
          background: 'var(--accent-emerald-subtle)',
          border: '1px solid var(--accent-emerald-border)',
          color: 'var(--accent-emerald)',
          padding: '0.2rem 0.55rem',
          borderRadius: 'var(--radius-full)',
          fontSize: '0.7rem',
          fontWeight: 600
        }}>
          <span className="status-dot"></span>
          <span>{slots.length} Real-Time Slots ({timeZone})</span>
        </div>
      </div>

      {slots.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '1.5rem 1rem' }}>
          <p style={{ fontSize: '0.84rem', color: 'var(--text-muted)', marginBottom: '0.75rem' }}>
            No slots found for this exact window. You can view all upcoming availability directly on Cal.com.
          </p>
          <a
            href={bookingUrl}
            target="_blank"
            rel="noreferrer"
            className="btn-primary"
            style={{ textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}
          >
            <span>Open Cal.com Calendar</span>
            <ArrowUpRight size={13} />
          </a>
        </div>
      ) : (
        <>
          {/* Date Selector Tabs */}
          <div style={{
            display: 'flex',
            gap: '0.4rem',
            overflowX: 'auto',
            paddingBottom: '0.45rem',
            marginBottom: '0.75rem',
            scrollbarWidth: 'thin'
          }}>
            {dates.map((dateKey) => {
              const isSelected = selectedDate === dateKey;
              const { dayName, dayNum } = formatTabHeader(dateKey);
              const count = groupedSlots[dateKey]?.length || 0;

              return (
                <button
                  key={dateKey}
                  type="button"
                  onClick={() => setSelectedDate(dateKey)}
                  style={{
                    flexShrink: 0,
                    padding: '0.4rem 0.65rem',
                    borderRadius: 'var(--radius-md)',
                    border: isSelected ? '1px solid var(--accent-slate)' : '1px solid var(--border-hairline)',
                    background: isSelected ? 'var(--accent-slate)' : '#FFFFFF',
                    color: isSelected ? '#FFFFFF' : 'var(--text-primary)',
                    cursor: 'pointer',
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: '0.1rem',
                    minWidth: '72px',
                    transition: 'all 0.12s ease',
                    boxShadow: isSelected ? '0 1px 3px rgba(0, 0, 0, 0.12)' : 'var(--shadow-xs)'
                  }}
                >
                  <span style={{ fontSize: '0.65rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', opacity: isSelected ? 0.8 : 0.6 }}>
                    {dayName}
                  </span>
                  <span style={{ fontSize: '0.82rem', fontWeight: 700 }}>
                    {dayNum}
                  </span>
                  <span style={{
                    fontSize: '0.62rem',
                    color: isSelected ? '#FFFFFF' : 'var(--text-muted)',
                    fontWeight: 500,
                    opacity: isSelected ? 0.9 : 1
                  }}>
                    {count} slots
                  </span>
                </button>
              );
            })}
          </div>

          {/* Time Slots Grid */}
          <div style={{
            background: 'var(--bg-surface-subtle)',
            borderRadius: 'var(--radius-md)',
            border: '1px solid var(--border-hairline)',
            padding: '0.75rem',
            marginBottom: '0.75rem'
          }}>
            <div style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '0.55rem',
              flexWrap: 'wrap',
              gap: '0.3rem'
            }}>
              <span style={{ fontSize: '0.7rem', fontWeight: 700, color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Select a Time for {selectedDate}:
              </span>
              <span style={{ fontSize: '0.7rem', color: 'var(--text-muted)', fontWeight: 500 }}>
                All times in {timeZone}
              </span>
            </div>

            <div style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(100px, 1fr))',
              gap: '0.4rem',
              maxHeight: '190px',
              overflowY: 'auto',
              paddingRight: '0.2rem'
            }}>
              {activeSlots.map((slot, idx) => {
                const timeDisplay = formatSlotTime(slot, timeZone);
                const isSelected = selectedSlot?.time_utc === slot.time_utc;
                const isCurrentBooking = isSelected && isBooking;

                return (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => handleSlotClick(slot)}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: '0.3rem',
                      background: isSelected ? 'var(--accent-emerald-subtle)' : '#FFFFFF',
                      color: isSelected ? '#065F46' : 'var(--text-primary)',
                      border: isSelected ? '1px solid var(--accent-emerald-border)' : '1px solid var(--border-hairline)',
                      padding: '0.4rem 0.5rem',
                      borderRadius: 'var(--radius-sm)',
                      fontSize: '0.75rem',
                      fontWeight: 600,
                      cursor: 'pointer',
                      transition: 'all 0.12s ease',
                      textAlign: 'center',
                      boxShadow: 'var(--shadow-xs)'
                    }}
                    onMouseEnter={(e) => {
                      if (!isSelected) {
                        e.currentTarget.style.borderColor = 'var(--accent-slate)';
                        e.currentTarget.style.background = 'var(--bg-surface-hover)';
                        e.currentTarget.style.transform = 'translateY(-1px)';
                      }
                    }}
                    onMouseLeave={(e) => {
                      if (!isSelected) {
                        e.currentTarget.style.borderColor = 'var(--border-hairline)';
                        e.currentTarget.style.background = '#FFFFFF';
                        e.currentTarget.style.transform = 'translateY(0)';
                      }
                    }}
                    title={`Select ${slot.formatted_time || timeDisplay}`}
                  >
                    {isCurrentBooking ? (
                      <Loader2 size={11} className="spin" color="var(--accent-emerald)" />
                    ) : isSelected ? (
                      <CheckCircle2 size={11} color="var(--accent-emerald)" />
                    ) : (
                      <Clock size={11} color="var(--text-muted)" />
                    )}
                    <span>{timeDisplay}</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Active Slot Confirmation / Status Banner */}
          {selectedSlot && (
            <div style={{
              padding: '0.65rem 0.85rem',
              background: 'var(--accent-emerald-subtle)',
              border: '1px solid var(--accent-emerald-border)',
              borderRadius: 'var(--radius-md)',
              color: '#065F46',
              fontSize: '0.78125rem',
              fontWeight: 600,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: '0.65rem',
              animation: 'slideUpFade 0.15s ease-in-out',
              flexWrap: 'wrap',
              gap: '0.45rem'
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', flex: 1, minWidth: '200px' }}>
                <CheckCircle2 size={14} color="var(--accent-emerald)" />
                <span>Selected: <strong>{selectedSlot.formatted_time || formatSlotTime(selectedSlot, timeZone)}</strong></span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                <button
                  type="button"
                  disabled={isBooking}
                  onClick={() => handleSlotClick(selectedSlot)}
                  style={{
                    color: '#FFFFFF',
                    fontSize: '0.72rem',
                    fontWeight: 700,
                    background: 'var(--accent-slate)',
                    border: '1px solid rgba(255, 255, 255, 0.1)',
                    cursor: isBooking ? 'default' : 'pointer',
                    padding: '0.32rem 0.7rem',
                    borderRadius: 'var(--radius-sm)',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '0.3rem',
                    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.12)',
                    transition: 'all 0.12s ease'
                  }}
                >
                  {isBooking ? (
                    <>
                      <Loader2 size={11} className="spin" />
                      <span>Booking via Digital Twin...</span>
                    </>
                  ) : (
                    <span>Book via Digital Twin</span>
                  )}
                </button>
                <button
                  type="button"
                  onClick={handleClearSelection}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#065F46',
                    cursor: 'pointer',
                    padding: '0.15rem',
                    borderRadius: '4px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    opacity: 0.75
                  }}
                  title="Clear selection"
                >
                  <X size={13} />
                </button>
              </div>
            </div>
          )}

          {/* Footer Action */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '0.75rem',
            paddingTop: '0.2rem'
          }}>
            <span style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
              Want a 15m or 60m session instead?
            </span>
            <a
              href={bookingUrl}
              target="_blank"
              rel="noreferrer"
              style={{
                fontSize: '0.75rem',
                fontWeight: 600,
                color: 'var(--text-primary)',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.2rem',
                textDecoration: 'none'
              }}
            >
              <span>Open Cal.com Calendar</span>
              <ChevronRight size={13} />
            </a>
          </div>
        </>
      )}
    </div>
  );
};
