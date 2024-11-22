# UnityMemorySnapshotThing

Tool to analyze unity memory snapshot files (`*.snapshot`) outside of unity, and find leaked managed shells.

Also includes a library for you to work with these files yourself.

The primary use case of the executables in the published releases is to be run with a path to a unity memory snapshot. It will parse the snapshot and output all objects it can find that are what Unity calls "Leaked Managed Shells". These are Unity objects that have been destroyed but are still referenced from c# code, and so act as a memory leak. 

This tool is - in my opinion - a better workflow than the official Memory Profiler for a couple of reasons:
- It shows the direct retention path that's causing an object to be loaded.
- It's a *lot* faster than the in-editor profiler. In my tests, a dump that takes the editor ~90 seconds to load, and a further 30 seconds to filter to the leaked objects, takes less than 10 seconds to be processed by this tool.
- It filters out the noise and only shows you leaked objects.
- It shows you which specific fields are causing an object to be referenced, instead of just showing which objects reference it.
- It uses a lot less memory than the editor. Dumps that cause my editor to use in excess of 14gb of memory (while themselves being ~800mb) only take a couple GB to process.

But there are also a couple reasons you might need to use the in-editor one over this tool. Primarily:
- This tool doesn't show non-leaked shells, so it's useless for e.g. showing usage by category.
- This tool doesn't calculate how *much* memory is leaked, just how many objects.
- This tool doesn't allow comparing two snapshots.
- This tool may not have full support for snapshots greater than 2GiB in size. It works in theory but may not catch everything.
