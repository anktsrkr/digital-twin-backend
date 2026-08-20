-- ==============================================================================
-- 01_init_schema.sql
-- Conversational Digital Twin Resume Assistant (.NET 10 + Supabase pgvector)
-- Database schema, extensions, tables, vector index, and RPC match function
-- ==============================================================================

-- 1. Enable the pgvector extension for dense vector similarity operations
create extension if not exists vector;

-- 2. Resume Chunks Table (stores 1024-dimensional embeddings matching Voyage AI voyage-3-lite / voyage-3)
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
-- m=16, ef_construction=64 for sub-millisecond retrieval
create index if not exists idx_resume_chunks_embedding 
on public.resume_chunks 
using hnsw (embedding vector_cosine_ops)
with (m = 16, ef_construction = 64);

-- 4. B-Tree index for filtered queries (by category or company)
create index if not exists idx_resume_chunks_category on public.resume_chunks(category);
create index if not exists idx_resume_chunks_company on public.resume_chunks(company);

-- 5. Match Resume Chunks RPC Function (called from .NET 10 IVoyageRagSearcher)
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

-- 6. Recruiter Profiles Table (Lead capture and session tracking with Logto user IDs)
create table if not exists public.recruiter_profiles (
    id text primary key,
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
    recruiter_id text references public.recruiter_profiles(id) on delete set null,
    session_id text not null,
    role text not null, -- 'user', 'assistant', 'system'
    content text not null,
    cited_chunk_ids uuid[],
    tokens_prompt int default 0,
    tokens_completion int default 0,
    response_latency_ms int default 0,
    created_at timestamptz default now()
);

-- 8. Disposable Email Domains Blocklist Table
create table if not exists public.disposable_email_domains (
    domain text primary key,
    created_at timestamptz default now()
);
