﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories.Impl;

public class MusicPlaylistRepository : Repository<MusicPlaylist>, IMusicPlaylistRepository
{
    public MusicPlaylistRepository(DbContext context) : base(context)
    {
    }

    public List<MusicPlaylist> GetPlaylistsOnPage(int num)
    {
        if (num < 1)
            throw new IndexOutOfRangeException();

        return _set.AsQueryable()
            .Skip((num - 1) * 20)
            .Take(20)
            .Include(pl => pl.Songs)
            .ToList();
    }

    public MusicPlaylist GetWithSongs(int id)
    {
        return _set.Include(mpl => mpl.Songs)
            .FirstOrDefault(mpl => mpl.Id == id);
    }
}