# AutoThreadSafe

This library is for generating thread-safe versions of classes. It generates mutexes around methods and properties to ensure only one thread can access the method/property at a time. I call this process Weaving.

Weaving is flexible enough that you can specify different mutexes for different sets of methods and properties. It also has a simple mode for using one mutex for all access to the class.

## Work in progress

This is a work in progress. I worked on it a few months ago without quite completing it. I decided to tidy it up and get it on GitHub for visibility and version control. Use at your own risk, as there are no tests yet in place.
