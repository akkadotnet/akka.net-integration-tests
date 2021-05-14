# Akka.PersistentCart
This project is ment to serve as a test platform to test persistence serialization compatibility between different Akka.Persistence versions.
It is a simple application that persists and reads back shopping cart data into a local SQLite database.

## Abstract
This application is effective for testing persistence serialization compatibility between different versions of Akka.

## Running PersistentCart
This is ment to be run from the IDE to quickly swap different builds of Akka.Persistence.Sqlite nuget package and test if each of them can successfully persist 
and restore data serialized into a single database from each other.

1. Compile and run the application with one version of Akka.Persistence.Sqlite nuget package.
2. Persist a few cart records.
3. Compile and run the application with another version of Akka.Persistence.Sqlite nuget package that depends on another version of Akka.Persistence.
4. Try to load the previously persisted data and save a few more records.
5. Switch back to the first nuget package and compile and run the application again.
6. Check that all the records can be read back.