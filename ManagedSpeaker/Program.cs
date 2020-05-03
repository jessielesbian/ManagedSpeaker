using System;
using NAudio.Wave;
using System.Threading;
using MP3Sharp;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Security.Cryptography;
using GoogleTranslateFreeApi;
using System.Net;
using System.Collections;

namespace jessielesbian.ManagedSpeaker
{
	//ManagedSpeaker: an open-source text-to-speech engine made entirely out of c#
	//Made by Jessie Lesbian <jessielesbian@protonmail.com> https://www.reddit.com/u/jessielesbian
	//This software is proudly made by LGBT programmers!
	public static class Program
	{
		public static void Main(string[] args)
		{
			if(!Directory.Exists(Utils.dictionaryPath))
			{
				Directory.CreateDirectory(Utils.dictionaryPath);
			}
			Console.WriteLine("ManagedSpeaker: an open-source text-to-speech engine made entirely out of c#");
			Console.WriteLine("Made by Jessie Lesbian <jessielesbian@protonmail.com> https://www.reddit.com/u/jessielesbian");
			Console.WriteLine("This software is proudly made by LGBT programmers!");
			Console.WriteLine();
			int length = args.Length;
			if(length == 0)
			{
				args = new string[] {""};
			}
			switch(args[0]) {
				case "SelfTest":
					Utils.Speak("Hi, this is Jessie Lesbian. This is one of my code projects, ManagedSpeaker, an open-source text-to-speech engine made entirely out of c sharp.").Play();
					break;
				case "SAY2SPK":
					if(length != 2)
					{
						goto default;
					}
					Utils.Speak(args[1]).Play();
					break;
				case "SAY2WAV":
					if(length != 3)
					{
						goto default;
					}
					LargeMemoryStream largeMemoryStream = Utils.Speak(args[2]);
					FileStream fileStream = new FileStream(args[1], FileMode.Create, FileAccess.Write);
					WaveFileWriter waveFileWriter = new WaveFileWriter(fileStream, largeMemoryStream.WaveFormat);
					largeMemoryStream.CopyTo(waveFileWriter);
					largeMemoryStream.Dispose();
					waveFileWriter.Flush();
					waveFileWriter.Dispose();
					break;
				case "RMWORD":
					if(length != 2)
					{
						goto default;
					}
					File.Delete(Utils.dictionaryPath + BitConverter.ToString(Utils.sha256.ComputeHash(Encoding.UTF8.GetBytes(args[1].ToLower()))).Replace("-", ""));
					break;
				case "CLRDICT":
					Parallel.ForEach(Directory.GetFiles(Utils.dictionaryPath), (string s) => File.Delete(s));
					break;
				default:
					Console.WriteLine("USAGE:");
					Console.WriteLine();
					Console.WriteLine("to say something");
					Console.WriteLine("ManagedSpeaker SAY2SPK <thing>");
					Console.WriteLine();
					Console.WriteLine("to say something and then export it as WAV");
					Console.WriteLine("ManagedSpeaker SAY2WAV <output> <thing>");
					Console.WriteLine();
					Console.WriteLine("to run self test");
					Console.WriteLine("ManagedSpeaker SelfTest");
					Console.WriteLine();
					Console.WriteLine("to delete a broken word from dictionary");
					Console.WriteLine("ManagedSpeaker RMWORD <word>");
					Console.WriteLine();
					Console.WriteLine("to clear the dictionary");
					Console.WriteLine("ManagedSpeaker CLRDICT");
					break;
			}
		}
	}
	public sealed class LargeMemoryStream : GenericWaveBuffer
	{
		//I mean, LARGE AND FAST!!!
		private byte[] mempool = new byte[0];

		private long preallocated = 0;

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => true;

		public override long Length => mempool.LongLength - preallocated;

		private long position = 0;

		public override long Position {
			get => position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public override void Flush()
		{

		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int i = 0;
			lock(this)
			{
				for(i = 0; i < count; i++)
				{
					long index = i + offset;
					long length = Length;
					if(Position + 1 >= Length)
					{
						return i;
					}
					buffer[index] = mempool[Position];
					Position++;
				}
			}
			return i;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			lock(this)
			{
				long length = Length;
				switch(origin)
				{
					case SeekOrigin.Begin:
						if(offset > length)
						{
							throw new IndexOutOfRangeException();
						}
						position = offset;
						break;
					case SeekOrigin.End:
						if(offset > length)
						{
							throw new IndexOutOfRangeException();
						}
						position = length - 1 - offset;
						break;
					case SeekOrigin.Current:
						position = position + offset;
						if(position > length)
						{
							throw new IndexOutOfRangeException();
						}
						if(0 > position)
						{
							throw new IndexOutOfRangeException();
						}
						break;
				}
				return position;
			}

		}

		public override void SetLength(long value)
		{
			lock(this)
			{
				if(value < Length)
				{
					byte[] bytes = new byte[Length + value + preallocated];
					mempool.CopyTo(bytes, 0);
					mempool = bytes;
				}
				else
				{
					throw new IndexOutOfRangeException();
				}
			}
		}

		public void Preallocate(long length)
		{
			lock(this)
			{
				long oldLength = Length;
				preallocated += length;
				byte[] bytes = new byte[oldLength + preallocated];
				mempool.CopyTo(bytes, 0);
				mempool = bytes;
			}
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Preallocate(count);
			lock(this)
			{
				for(int i = 0; i < count; i++)
				{
					WriteByte(buffer[i]);
				}
			}
		}

		public override void WriteByte(byte value)
		{
			lock(this)
			{
				while((Position + 1) >= Length)
				{
					if(preallocated > 0)
					{
						preallocated--;
					}
					else
					{
						byte[] bytes = new byte[Length + 1 + preallocated];
						mempool.CopyTo(bytes, 0);
						mempool = bytes;
					}
				}
				mempool[Position++] = value;
				if(preallocated < 2048)
				{
					Preallocate(4096);
				}
			}
		}

		//extensions

		public LargeMemoryStream() : base()
		{
		
		}

		public LargeMemoryStream(WaveFormat waveFormat) : base(waveFormat)
		{
			
		}
	}
	public abstract class GenericWaveBuffer : Stream, IWaveProvider
	{
		public GenericWaveBuffer()
		{
			WaveFormat = new WaveFormat();
		}
		public GenericWaveBuffer(WaveFormat waveFormat)
		{
			WaveFormat = waveFormat;
		}
		public virtual WaveFormat WaveFormat
		{
			get;
		}
	}

	public static class Utils
	{
		static Utils()
		{
			string EXEPath = Assembly.GetAssembly(typeof(LargeMemoryStream)).Location;
			dictionaryPath = EXEPath.Substring(0, EXEPath.LastIndexOf("\\")) + "\\Dictionary\\";
			waveFormat = ConstructWord("nothing").WaveFormat;
		}
		public static readonly string dictionaryPath;
		public static readonly SHA256 sha256 = SHA256CryptoServiceProvider.Create();
		public static readonly GoogleKeyTokenGenerator googleKeyTokenGenerator = new GoogleKeyTokenGenerator();
		public static readonly WaveFormat waveFormat;
		public static List<ushort> To16BitWaveArray(this IWaveProvider waveProvider)
		{
			LargeMemoryStream largeMemoryStream = new LargeMemoryStream(waveProvider.WaveFormat);
			waveProvider.CopyTo(largeMemoryStream);
			largeMemoryStream.Position = 0;
			BinaryReader binaryReader = new BinaryReader(largeMemoryStream);
			long length = largeMemoryStream.Length / 2;
			List<ushort> shorts = new List<ushort>((int) length);
			for(long i = 0; i < length; i++)
			{
				shorts.Add(binaryReader.ReadUInt16());
			}
			binaryReader.Dispose();
			return shorts;
		}
		public static void Append16BitWaveArray(this GenericWaveBuffer genericWaveBuffer, List<ushort> waveArray)
		{
			BinaryWriter binaryWriter = new BinaryWriter(genericWaveBuffer, Encoding.UTF8, true);
			foreach(ushort wave in waveArray)
			{
				binaryWriter.Write(wave);
			}
			binaryWriter.Dispose();
		}
		public static LargeMemoryStream ConstructWord(string word, int seed = 0)
		{
			word = word.ToLower();
			FileStream fileStream = new FileStream(dictionaryPath + BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(word))).Replace("-", ""), FileMode.OpenOrCreate, FileAccess.ReadWrite);
			if(fileStream.Length == 0)
			{
				WebClient webClient = new WebClient();
				Stream webStream = webClient.OpenRead("https://translate.google.com/translate_tts?ie=UTF-8&tl=en&total=1&idx=0&textlen=7&client=webapp&prev=input" + "&q=" + word + "&tk=" + Await(googleKeyTokenGenerator.GenerateAsync(word)));
				webStream.CopyTo(fileStream);
				webStream.Dispose();
				fileStream.Position = 0;
				fileStream.Flush();
			}
			MP3Stream stream = new MP3Stream(fileStream);
			WaveFormat waveFormat = new WaveFormat(stream.Frequency, 2);
			LargeMemoryStream largeMemoryStream = new LargeMemoryStream(waveFormat);
			stream.CopyTo(largeMemoryStream);
			stream.Dispose();
			fileStream.Dispose();
			largeMemoryStream.Position = 0;
			List<ushort> shorts = largeMemoryStream.To16BitWaveArray();
			largeMemoryStream.Dispose();
			int length = shorts.Count;
			int half = length / 2;
			List<ushort> left = new List<ushort>(half);
			List<ushort> right = new List<ushort>(half);
			for(int i = 0; i < length; i++) {
				if((i % 2) == 1)
				{
					left.Add(shorts[i]);
				}
				else
				{
					right.Add(shorts[i]);
				}
			}
			half = Math.Min(left.Count, right.Count);
			Random random = new Random(seed);
			int removals = (int)Math.Ceiling(Math.Sqrt(half));
			for(int i = 0; i < removals; i++)
			{
				left.RemoveAt(random.Next() % left.Count);
				right.RemoveAt(random.Next() % right.Count);
			}
			half = Math.Min(left.Count, right.Count);
			removals = Math.Min(half / 5, removals);
			for(int i = 0; i < removals; i++)
			{
				left.RemoveAt(0);
				right.RemoveAt(0);
				left.RemoveAt(left.Count - 1);
				right.RemoveAt(right.Count - 1);
			}
			half = Math.Min(left.Count, right.Count);
			length = half * 2;
			shorts = new List<ushort>(length);
			for(int i = 0; i < half; i++)
			{
				shorts.Add(right[i]);
				shorts.Add(left[i]);
			}
			largeMemoryStream = new LargeMemoryStream(waveFormat);
			largeMemoryStream.Append16BitWaveArray(shorts);
			largeMemoryStream.Position = 0;
			return largeMemoryStream;
		}
		public static void CopyTo(this IWaveProvider waveProvider, Stream stream)
		{
			byte[] buffer = new byte[65536];
			int read;
			while((read = waveProvider.Read(buffer, 0, buffer.Length)) != 0)
			{
				stream.Write(buffer, 0, read);
			}
		}
		public static LargeMemoryStream Speak(string text)
		{
			string[] words = text.Split(' ');
			int seed = text.GetHashCode();
			int length = words.Length;
			Dictionary<int, LargeMemoryStream> largeMemoryStreams = new Dictionary<int, LargeMemoryStream>(length);
			Parallel.For(0, length, (int i) => {
				LargeMemoryStream pending = ConstructWord(words[i].Replace(".", "").Replace(",", ""), seed + i);
				lock(largeMemoryStreams)
				{
					largeMemoryStreams.Add(i, pending);
				}
			});
			LargeMemoryStream largeMemoryStream = new LargeMemoryStream(waveFormat);
			for(int c = 0; c < length; c++) {
				LargeMemoryStream temp = largeMemoryStreams[c];
				string word = words[c];
				temp.CopyTo(largeMemoryStream);
				temp.Dispose();
				if(word.EndsWith("."))
				{
					for(int i = 0; i < 100000; i++)
					{
						largeMemoryStream.WriteByte(0);
					}
				}
				else if(word.EndsWith(","))
				{
					for(int i = 0; i < 50000; i++)
					{
						largeMemoryStream.WriteByte(0);
					}
				}
			}
			largeMemoryStream.Position = 0;
			return largeMemoryStream;
		}
		private static type Await<type>(Task<type> task)
		{
			ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim(false);
			task.GetAwaiter().OnCompleted(() => manualResetEventSlim.Set());
			manualResetEventSlim.Wait();
			Exception exception = task.Exception;
			if(exception != null)
			{
				throw exception;
			}
			return task.Result;
		}
		public static void Play(this GenericWaveBuffer genericWaveBuffer)
		{
			WaveOutEvent waveOutEvent = new WaveOutEvent();
			waveOutEvent.Init(genericWaveBuffer);
			waveOutEvent.Volume = 1;
			ManualResetEventSlim manualResetEvent = new ManualResetEventSlim(false);
			waveOutEvent.PlaybackStopped += (object sender, StoppedEventArgs e) => {
				manualResetEvent.Set();
			};
			waveOutEvent.Play();
			manualResetEvent.Wait();
			waveOutEvent.Dispose();
		}
	}
}