namespace DetourSharp.Hosting;

/// <summary>A 64-bit pointer to a thread start routine.</summary>
unsafe readonly struct ThreadStartPtr64<T>
    where T : unmanaged
{
    readonly ulong value;

    /// <summary>Gets the pointer as an unmanaged delegate.</summary>
    public delegate* unmanaged<T*, uint> Invoke => this;

    /// <summary>Initializes a new <see cref="ThreadStartPtr64{T}"/> instance.</summary>
    public ThreadStartPtr64(delegate* unmanaged<T*, uint> address) => value = (ulong)address;

    /// <summary>Defines an implicit conversion from <see cref="ThreadStartPtr64{T}"/> to <see cref="void"/>*.</summary>
    public static implicit operator void*(ThreadStartPtr64<T> ptr) => (void*)ptr.value;

    /// <summary>Defines an explicit conversion from <see cref="void"/>* to <see cref="ThreadStartPtr64{T}"/>.</summary>
    public static explicit operator ThreadStartPtr64<T>(void* ptr) => new((delegate* unmanaged<T*, uint>)ptr);

    /// <summary>Defines an implicit conversion from <see cref="ThreadStartPtr64{T}"/> to <see langword="delegate"/>* <see langword="unmanaged"/>&lt;<typeparamref name="T"/>*, <see cref="uint"/>&gt;.</summary>
    public static implicit operator delegate* unmanaged<T*, uint>(ThreadStartPtr64<T> ptr) => (delegate* unmanaged<T*, uint>)ptr.value;

    /// <summary>Defines an implicit conversion from <see langword="delegate"/>* <see langword="unmanaged"/>&lt;<typeparamref name="T"/>*, <see cref="uint"/>&gt; to <see cref="ThreadStartPtr64{T}"/>.</summary>
    public static implicit operator ThreadStartPtr64<T>(delegate* unmanaged<T*, uint> ptr) => new(ptr);

    /// <summary>Defines an implicit conversion from <see cref="ThreadStartPtr64{T}"/> to <see cref="ThreadStartPtr64"/>.</summary>
    public static implicit operator ThreadStartPtr64(ThreadStartPtr64<T> ptr) => new((delegate* unmanaged<void*, uint>)ptr.value);

    /// <summary>Defines an explicit conversion from <see cref="ThreadStartPtr64"/> to <see cref="ThreadStartPtr64{T}"/>.</summary>
    public static explicit operator ThreadStartPtr64<T>(ThreadStartPtr64 ptr) => new((delegate* unmanaged<T*, uint>)(void*)ptr);
}
