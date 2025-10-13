# Workshop Speaker Notes - Quick Reference Guide

This folder contains detailed speaker notes for the **Couchbase Lite RAG Workshop**. These notes are designed to help you deliver an engaging, comprehensive workshop on building RAG applications with vector search.

---

## 📚 Files Overview

| File | Topic | Duration | Key Concepts |
|------|-------|----------|--------------|
| **Exercise1_SpeakerNotes.md** | Database Initialization | ~15 min | CouchbaseLite.init(), Vector Search extension, Object singleton |
| **Exercise2_SpeakerNotes.md** | Gemini API Integration | ~20 min | LLM parameters, Coroutines, suspend functions, API configuration |
| **Exercise3_SpeakerNotes.md** | Document Storage & Embeddings | ~25 min | ONNX models, Vector storage, QueryBuilder, Kotlin Flows |
| **Exercise4_SpeakerNotes.md** | Complete RAG Pipeline | ~30 min | Vector indexing, Similarity search, RAG implementation, Error handling |

**Total Workshop Duration:** ~90 minutes (1.5 hours)

---

## 🎯 Workshop Flow

```
Setup & Intro (10 min)
    ↓
Exercise 1: Database Setup (15 min)
    ↓
Exercise 2: LLM Integration (20 min)
    ↓
Break (10 min)
    ↓
Exercise 3: Embeddings & Storage (25 min)
    ↓
Exercise 4: RAG Pipeline (30 min)
    ↓
Q&A & Wrap-up (10 min)

Total: ~2 hours
```

---

## 📖 How to Use These Notes

### Before the Workshop:

1. **Read all files thoroughly**
   - Understand the complete learning arc
   - Familiarize yourself with code changes
   - Practice explanations

2. **Set up demo environment**
   - Clone repo on your machine
   - Test each branch works
   - Prepare sample documents
   - Verify Gemini API key

3. **Prepare visuals**
   - Whiteboard for RAG pipeline diagram
   - Slides for embedding visualization
   - Architecture diagrams

### During the Workshop:

1. **Keep notes open** on second screen/tablet
2. **Follow the structure** but adapt to audience
3. **Use the troubleshooting sections** when students have issues
4. **Reference discussion points** to engage students
5. **Check success criteria** before moving to next exercise

### After Each Exercise:

- ✅ Verify all students completed successfully
- ❓ Address common issues from notes
- 💬 Run interactive discussion
- ⏱️ Stay on time (use time management guide)

---

## 🎤 Quick Reference by Section

### Exercise 1: Database Initialization

**Key Message:** "We're replacing mock objects with real Couchbase Lite"

**Main Code Change:** `DatabaseManager.kt` - Uncomment real implementation

**Critical Concepts:**
- `CouchbaseLite.init()` must be first
- `applicationContext` vs Activity context
- `enableVectorSearch()` may fail gracefully
- Object singleton pattern

**Success Check:** 3 log messages in Logcat

---

### Exercise 2: Gemini API Integration

**Key Message:** "We're adding AI capabilities with Gemini, but it needs context"

**Main Code Changes:**
- `GeminiRemoteAPI.kt` - Activate API calls
- `MainActivity.kt` - Add database init + test

**Critical Concepts:**
- Temperature & topP parameters
- `suspend` functions and coroutines
- `withContext(Dispatchers.IO)`
- BuildConfig for secrets

**Success Check:** "Paris" response in Logcat

---

### Exercise 3: Document Storage & Embeddings

**Key Message:** "We're storing documents with their semantic embeddings"

**Main Code Changes:**
- `DocumentsDB.kt` - CRUD operations
- `ChunksDB.kt` - Chunk storage (NOT search yet)
- `ChunksUseCase.kt` - Embedding generation

**Critical Concepts:**
- ONNX local models
- 384-dimensional embeddings
- Document-chunk relationships
- FloatArray ↔ MutableArray conversion

**Success Check:** App compiles, no crashes

---

### Exercise 4: Complete RAG Pipeline

**Key Message:** "Now we connect everything - the magic of RAG!"

**Main Code Changes:**
- `ChunksDB.kt` - Vector index + search
- `ChunksUseCase.kt` - Query embeddings
- `QAUseCase.kt` - Full RAG pipeline

**Critical Concepts:**
- VectorIndexConfiguration (384 dims, 3 centroids)
- APPROX_VECTOR_DISTANCE SQL function
- Retrieval → Augmentation → Generation
- Thread switching (IO → Main)
- Error handling

**Success Check:** Upload doc → Ask question → Get grounded answer

---

## 💡 Teaching Tips

### Visual Aids That Work:

1. **RAG Pipeline Flowchart** (Draw on whiteboard)
```
User Query → Embedding → Vector Search → Top 5 Chunks → 
Context Building → LLM Prompt → Gemini API → Answer
```

2. **Vector Similarity Visualization**
```
Query: [0.2, 0.5, ...]  ←─── Distance: 0.3 (similar)
Chunk1: [0.21, 0.51, ...]

Query: [0.2, 0.5, ...]  ←─── Distance: 1.8 (dissimilar)
Chunk2: [-0.8, -0.3, ...]
```

3. **Thread Diagram**
```
Main Thread ─────┐
                 ↓ Switch to IO
IO Thread ───────── Network call
                 ↑ Switch back
Main Thread ─────┘ Update UI
```

### Engagement Strategies:

1. **Start each exercise with a question**
   - "Who knows what temperature means in AI?"
   - "Why do we need embeddings?"

2. **Live coding vs Copy-paste**
   - Exercise 1-2: Live code (foundational)
   - Exercise 3-4: Can copy (more complex)

3. **Predict then verify**
   - "What do you think this log will say?"
   - "How many dimensions should we see?"

4. **Compare alternatives**
   - "What if we used GPT-4 instead?"
   - "Why local ONNX instead of API?"

### Common Student Questions:

Prepared answers in each exercise file's "Discussion Points" section!

---

## 🐛 Troubleshooting Quick Reference

### Issue: App won't build
- Check: Gradle sync completed
- Check: API key in `local.properties`
- Check: Internet connection for dependencies

### Issue: Database init fails
- Check: `CouchbaseLite.init()` called first
- Check: Using `applicationContext` not Activity
- Check: Permissions granted

### Issue: Vector search returns empty
- Check: Documents uploaded successfully
- Check: Embeddings are 384 dimensions
- Check: Vector index created (log message)
- Check: Query embedding generated

### Issue: Generic LLM answers
- Check: Context retrieved (log shows chunks)
- Check: Context inserted in prompt
- Check: Prompt template has `$CONTEXT` and `$QUERY`

### Issue: App crashes on question
- Check: Running on Main thread (callback)
- Check: Error handling in place
- Check: Internet available

---

## 📊 Time Management Tips

### If Running Behind:

**Shorten Exercise 1:**
- Skip detailed explanation of Vector Search extension
- Just show it enables vector features
- **Save:** 3 min

**Shorten Exercise 2:**
- Skip deep dive on temperature/topP
- Just say "optimized for Q&A"
- **Save:** 5 min

**Shorten Exercise 3:**
- Skip detailed Flow explanation
- Focus on embedding generation
- **Save:** 5 min

**Total savings:** 13 minutes

### If Running Ahead:

**Extend Exercise 4:**
- Deeper dive into IVF indexing
- Show alternative index configurations
- Discuss hybrid search strategies
- **Add:** 10-15 min

---

## 🎓 Learning Outcomes

By the end of this workshop, students will be able to:

✅ Initialize and configure Couchbase Lite on Android  
✅ Enable and use Vector Search extension  
✅ Integrate external LLM APIs (Gemini)  
✅ Generate embeddings using local ONNX models  
✅ Store and query high-dimensional vectors  
✅ Build a complete RAG pipeline  
✅ Handle async operations with coroutines  
✅ Implement proper error handling  
✅ Understand RAG architecture and trade-offs  
✅ Deploy a functional AI-powered Android app  

---

## 📚 Additional Resources to Share

**Official Documentation:**
- [Couchbase Lite Android](https://docs.couchbase.com/couchbase-lite/current/android/gs-install.html)
- [Vector Search Guide](https://docs.couchbase.com/couchbase-lite/current/android/vector-search.html)

**Conceptual:**
- [RAG Paper (Arxiv)](https://arxiv.org/abs/2005.11401)
- [Embeddings Explained](https://vickiboykis.com/what_are_embeddings/)
- [Vector Databases Overview](https://www.pinecone.io/learn/vector-database/)

**Practical:**
- [Kotlin Coroutines](https://kotlinlang.org/docs/coroutines-guide.html)
- [Jetpack Compose](https://developer.android.com/jetpack/compose)
- [ONNX Runtime](https://onnxruntime.ai/)

---

## 🎯 Workshop Goals

### Primary Goals:
1. Students build a working RAG app
2. Students understand RAG architecture
3. Students can explain key concepts (embeddings, vector search, RAG)

### Secondary Goals:
1. Exposure to modern Android development (Compose, Coroutines, Hilt)
2. Understanding of local-first AI architecture
3. Practical experience with vector databases

### Stretch Goals:
1. Students continue building on the project
2. Students share their work
3. Students apply concepts to their own projects

---

## 💬 Feedback & Iteration

After the workshop, note:
- Which explanations resonated
- Which sections took longer than expected
- Common questions that weren't covered
- Suggestions for improvement

These notes are living documents - update them based on your experience!

---

## 📞 Support

If you need help or have questions about these materials:
- Review the detailed section in each exercise file
- Check the troubleshooting sections
- Test the code changes on each branch
- Practice the explanations out loud

---

**Good luck with your workshop! You've got this! 🚀**

Remember: The best workshops are interactive, hands-on, and fun. Don't just lecture - engage your students, celebrate their successes, and help them when they struggle. They're learning cutting-edge AI technology!

---

*Last Updated: October 2025*
*Workshop Version: 1.0*
*Couchbase Lite Version: 3.2.0*

