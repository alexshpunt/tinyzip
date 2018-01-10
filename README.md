# TinyZip

## Summary

TinyZip is a library that provides a really fast way to get data directly from zip archives. 
It was implemented as a workaround for mobile file-systems issue. They are slow and really prone to fragmentation, but archives are really useful when you want to deliver all needed assets to user. Most zip libraries offer only packing/unpacking feature and there is no way to take just a single file. 
TinyZip supports compression detection, nested archives and folders. But it doesn't support ecnryption. 

## How to use 

It's called *tiny* cause it's a single class library, so you don't need much to start using it. 
The class is derived from IDisposable so it's highly recomended to use `using` block. If you don't want -- don't forget to release it with calling `Dispose()` method. 
If you read from some file you can use constructor that takes string containing file name. 
```C#
using( var archive = new TinyZip( "assets.zip" ) )
{
    ...
}
```
Or you can build an object with a bytes array, it's always useful if you want to read a file in a different way other
than .NET `FileStream`. 
```C#
var bytes = new byte[ 1024 ];
ReadBytes( bytes );
using( var archive = new TinyZip( bytes ) )
{
    ...
}
```
When archive is ready you can start to use it.
To get all files names which the archive contains you should use `FileNames` property. 
```C#
var fileNames = archive.FileNames; 
```
And then you can just iterate through them or search needed one and get file's data from the archive. 

```C#
foreach( var fileName in fileNames )
{
    var fileDescription = archive[ fileName ]; 
}
```
TinyZip returns not a plain bytes but a file description, so you can find out if the file was compressed and which compression method was used. 
TinyZip doesn't support decompression out of the box, so if you want to use compressed archives you need to add required libs to your project and make the decompression by yourself. 

```C#
if( fileDescription.compressionMethod == TinyZip.FileDescription.CompressionMethod.None )
{
    var readyToUseBytes = fileDescription.data; 
    ProcessData( readyToUseBytes );
}
else 
{
    switch( fileDescription.compressionMethod )
    {
        case TinyZip.FileDescription.CompressionMethod.Deflate:
            var readyToUseBytes = Delfate.Decompress( fileDescription.data ); //Just an example 
            ProcessData( readyToUseBytes );
        break;
        case ...
            ...
        break;
    }
}
```

To use nested archives you need to get data and send it to the bytes constructor.
```C#
using ( var archive = new TinyZip( data ) )
{
    var fileNames = archive.FileNames;
    foreach ( var fileName in fileNames )
    {
        if( fileName.EndsWith( ".zip" ) )
        {
            using( var nestedArchive = new TinyZip( archive[ fileName ].data ) )
            {
                //Work with it just as you do with a file archive. 
            }
            continue;
        }
        //Process files data 
    }
}
```
