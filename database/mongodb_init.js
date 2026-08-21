// MongoDB Initialization Script for Conversational Digital Twin Resume Assistant
// Sets up database collections and BSON secondary indexes

db = db.getSiblingDB('resume_assistant');

// 1. Resume Chunks Collection
db.createCollection('resume_chunks');
db.resume_chunks.createIndex({ "category": 1 });
db.resume_chunks.createIndex({ "company": 1 });

// 2. User Threads Collection (Persisted Conversations & AgentSessions)
db.createCollection('user_threads');
db.user_threads.createIndex({ "thread_id": 1 }, { unique: true });
db.user_threads.createIndex({ "user_id": 1 });
db.user_threads.createIndex({ "user_email": 1 });
db.user_threads.createIndex({ "last_updated_at": -1 });

// 3. Recruiter Profiles Collection
db.createCollection('recruiter_profiles');
db.recruiter_profiles.createIndex({ "email": 1 }, { unique: true });
db.recruiter_profiles.createIndex({ "domain": 1 });

print("MongoDB resume_assistant collections and indexes initialized successfully.");
