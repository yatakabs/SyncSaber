using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SyncSaber
{
    public class PlaylistSong
    {
        public string key;
        public string songName;

        public PlaylistSong(string songIndex, string songName)
        {
            this.key = songIndex;
            this.songName = songName;
        }
    }

    public class PlaylistIO
    {
        public static readonly string Path = "Playlists\\SyncSaberPlaylist.json";
        public static readonly string OldPath = "Playlists\\MapperFeedPlaylist.json";

        public static Playlist ReadPlaylist()
        {
            try
            {
                String json = File.ReadAllText(Path);
                Playlist playlist = new Playlist("SyncSaber Songs", "brian91292", null);

                JSONNode playlistNode = JSON.Parse(json);

                playlist.Image = playlistNode["image"];
                playlist.Title = playlistNode["playlistTitle"];
                playlist.Author = playlistNode["playlistAuthor"];
                playlist.Songs = new List<PlaylistSong>();

                foreach (JSONNode node in playlistNode["songs"].AsArray)
                {
                    playlist.Songs.Add(new PlaylistSong(node["key"], node["songName"]));
                }

                playlist.fileLoc = null;

                return playlist;
            }
            catch (Exception e)
            {
                Plugin.Log($"Exception parsing playlist: {e.ToString()}");
            }
            return null;
        }

        public static void WritePlaylist(Playlist playlist)
        {
            JSONNode playlistNode = new JSONObject();

            playlistNode.Add("playlistTitle", new JSONString(playlist.Title));
            playlistNode.Add("playlistAuthor", new JSONString(playlist.Author));
            playlistNode.Add("image", new JSONString(playlist.Image));

            JSONArray songArray = new JSONArray();
            try
            {
                foreach (PlaylistSong s in playlist.Songs)
                {
                    JSONObject songObject = new JSONObject();
                    songObject.Add("key", new JSONString(s.key));
                    songObject.Add("songName", new JSONString(s.songName));
                    songArray.Add(songObject);
                }
                playlistNode.Add("songs", songArray);
            }
            catch (Exception e)
            {
                Plugin.Log($"{e.ToString()}");
            }

            playlistNode.Add("fileLoc", new JSONString("1"));

            File.WriteAllText(Path, playlistNode.ToString());
        }
    }

    public class Playlist
    {
        public string Title;
        public string Author;
        public string Image;
        public List<PlaylistSong> Songs;
        public string fileLoc;

        public Playlist(string playlistTitle, string playlistAuthor, string image)
        {
            this.Title = playlistTitle;
            this.Author = playlistAuthor;
            this.Image = image;
            Songs = new List<PlaylistSong>();
            fileLoc = "";
        }
        
        public void Add(string songIndex, string songName)
        {
            Songs.Add(new PlaylistSong(songIndex, songName));
        }

        public void WritePlaylist()
        {
            PlaylistIO.WritePlaylist(this);
        }

        public bool ReadPlaylist()
        {
            try { if (File.Exists(PlaylistIO.OldPath)) File.Move(PlaylistIO.OldPath, PlaylistIO.Path); }
            catch (Exception) { File.Delete(PlaylistIO.OldPath); }

            if (File.Exists(PlaylistIO.Path))
            {
                var playlist = PlaylistIO.ReadPlaylist();
                if (playlist != null)
                {
                    this.Title = playlist.Title;
                    this.Author = playlist.Author;
                    this.Image = playlist.Image;
                    this.Songs = playlist.Songs;
                    this.fileLoc = playlist.fileLoc;
                    Plugin.Log("Success loading playlist!");
                    return true;
                }
            }
            return false;
        }
    }
}