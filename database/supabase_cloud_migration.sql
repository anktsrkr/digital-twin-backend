-- ==============================================================================
-- Supabase Cloud Production Migration Script
-- Project: Conversational Digital Twin Resume Assistant (.NET 10 + pgvector)
-- Run this in the Supabase Dashboard -> SQL Editor (postgres database)
-- ==============================================================================

-- 1. Enable pgvector extension for dense vector similarity operations
create extension if not exists vector with schema public;

-- 2. Resume Chunks Table (1024-dimensional embeddings matching Jina v3 / Voyage-3-lite)
create table if not exists public.resume_chunks (
    id uuid primary key default gen_random_uuid(),
    title text not null,
    category text not null, -- 'Experience', 'Education', 'Skills', 'Projects', 'Publications', 'Certifications', 'About'
    company text,
    role text,
    start_date text,
    end_date text,
    content text not null,
    source_name text not null,
    source_link text,
    technologies text[] default array[]::text[],
    embedding vector(1024) not null,
    created_at timestamptz default now(),
    updated_at timestamptz default now()
);

-- 3. High-Performance HNSW Index on Cosine Distance
create index if not exists idx_resume_chunks_embedding 
on public.resume_chunks 
using hnsw (embedding vector_cosine_ops)
with (m = 16, ef_construction = 64);

-- 4. B-Tree indexes for filtered queries
create index if not exists idx_resume_chunks_category on public.resume_chunks(category);
create index if not exists idx_resume_chunks_company on public.resume_chunks(company);

-- 5. Match Resume Chunks RPC Function (Called by .NET 10 SupabaseRagSearcher)
create or replace function public.match_resume_chunks (
    query_embedding vector(1024),
    match_count int default 10,
    filter_category text default null
)
returns table (
    id uuid,
    title text,
    category text,
    company text,
    role text,
    start_date text,
    end_date text,
    content text,
    source_name text,
    source_link text,
    technologies text[],
    similarity float
)
language plpgsql
stable
as $$
begin
    return query
    select
        rc.id,
        rc.title,
        rc.category,
        rc.company,
        rc.role,
        rc.start_date,
        rc.end_date,
        rc.content,
        rc.source_name,
        rc.source_link,
        rc.technologies,
        1 - (rc.embedding <=> query_embedding) as similarity
    from public.resume_chunks rc
    where filter_category is null or rc.category = filter_category
    order by rc.embedding <=> query_embedding
    limit match_count;
end;
$$;

-- 6. Recruiter Profiles Table (Lead capture and session tracking)
create table if not exists public.recruiter_profiles (
    id uuid primary key default gen_random_uuid(),
    email text not null unique,
    domain text not null,
    company_inferred text,
    first_login_at timestamptz default now(),
    last_active_at timestamptz default now(),
    total_messages int default 0,
    created_at timestamptz default now()
);

-- 7. Recruiter Conversations Audit Log Table
create table if not exists public.recruiter_conversations (
    id uuid primary key default gen_random_uuid(),
    recruiter_id uuid references public.recruiter_profiles(id) on delete set null,
    session_id text not null,
    query text not null,
    response text not null,
    citations jsonb default '[]'::jsonb,
    tokens_used int default 0,
    created_at timestamptz default now()
);

-- 8. Disposable Email Domains Blocklist Table
create table if not exists public.disposable_email_domains (
    domain text primary key,
    created_at timestamptz default now()
);

-- 9. Seed Disposable Domains
insert into public.disposable_email_domains (domain) values
('10minutemail.com'), ('10minutemail.net'), ('10minutemail.org'), ('10minmail.com'),
('20minutemail.com'), ('anonbox.net'), ('burnermail.io'), ('crazymailing.com'),
('dispostable.com'), ('dropmail.me'), ('emailondeck.com'), ('fakeinbox.com'),
('fakemailgenerator.com'), ('generator.email'), ('getairmail.com'), ('getnada.com'),
('guerrillamail.biz'), ('guerrillamail.com'), ('guerrillamail.de'), ('guerrillamail.net'),
('guerrillamail.org'), ('guerrillamailblock.com'), ('incognitomail.org'), ('inboxkitten.com'),
('maildrop.cc'), ('mailinator.com'), ('mailinator.net'), ('mailinator2.com'),
('mailnesia.com'), ('mailnull.com'), ('mohmal.com'), ('mytrashmail.com'),
('mytemp.email'), ('nada.ltd'), ('sharklasers.com'), ('spam4.me'),
('spambox.us'), ('spamfree24.org'), ('spamgourmet.com'), ('temp-mail.org'),
('tempmail.com'), ('tempmail.net'), ('tempmailaddress.com'), ('throwawaymail.com'),
('trashmail.com'), ('trashmail.net'), ('trashmail.org'), ('yopmail.com'),
('yopmail.fr'), ('yopmail.net'), ('zippymail.info'), ('disposablemail.com'),
('grr.la'), ('pokemail.net')
on conflict (domain) do nothing;

-- 10. Enable Row Level Security (RLS)
alter table public.resume_chunks enable row level security;
alter table public.recruiter_profiles enable row level security;
alter table public.recruiter_conversations enable row level security;
alter table public.disposable_email_domains enable row level security;

-- 11. Policies
-- Resume chunks: Read access for authenticated users & anon/service role
drop policy if exists "Allow read on resume chunks" on public.resume_chunks;
create policy "Allow read on resume chunks"
on public.resume_chunks for select
using (true);

-- Recruiter profiles: Users can view/insert/update own profile or service role
drop policy if exists "Users can view own recruiter profile" on public.recruiter_profiles;
create policy "Users can view own recruiter profile"
on public.recruiter_profiles for select
using (auth.uid() = id or auth.uid() is null);

drop policy if exists "Users can insert own recruiter profile" on public.recruiter_profiles;
create policy "Users can insert own recruiter profile"
on public.recruiter_profiles for insert
with check (auth.uid() = id or auth.uid() is null);

drop policy if exists "Users can update own recruiter profile" on public.recruiter_profiles;
create policy "Users can update own recruiter profile"
on public.recruiter_profiles for update
using (auth.uid() = id or auth.uid() is null);

-- Recruiter conversations: Read & Insert
drop policy if exists "Users can view own conversations" on public.recruiter_conversations;
create policy "Users can view own conversations"
on public.recruiter_conversations for select
using (auth.uid() = recruiter_id or auth.uid() is null);

drop policy if exists "Users can insert own conversations" on public.recruiter_conversations;
create policy "Users can insert own conversations"
on public.recruiter_conversations for insert
with check (auth.uid() = recruiter_id or auth.uid() is null);

-- Disposable domains: Public read
drop policy if exists "Allow read on disposable email domains" on public.disposable_email_domains;
create policy "Allow read on disposable email domains"
on public.disposable_email_domains for select
using (true);

-- 12. Trigger function to create recruiter_profile on auth.users sign-up
create or replace function public.handle_new_recruiter()
returns trigger
language plpgsql
security definer set search_path = public
as $$
declare
    user_email text;
    email_domain text;
    inferred_co text;
begin
    user_email := new.email;
    email_domain := split_part(user_email, '@', 2);
    
    if email_domain not in ('gmail.com', 'outlook.com', 'yahoo.com', 'hotmail.com', 'icloud.com', 'proton.me', 'protonmail.com') then
        inferred_co := initcap(split_part(email_domain, '.', 1));
    else
        inferred_co := null;
    end if;

    insert into public.recruiter_profiles (id, email, domain, company_inferred, first_login_at, last_active_at)
    values (new.id, user_email, email_domain, inferred_co, now(), now())
    on conflict (id) do update 
    set last_active_at = now();

    return new;
end;
$$;

-- Attach trigger to auth.users
drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_recruiter();
