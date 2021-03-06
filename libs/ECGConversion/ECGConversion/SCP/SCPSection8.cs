/***************************************************************************
Copyright 2012-2013, van Ettinger Information Technology, Lopik, The Netherlands
Copyright 2004,2008-2009, Thoraxcentrum, Erasmus MC, Rotterdam, The Netherlands

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Written by Maarten JB van Ettinger.

****************************************************************************/
using System;
using System.Runtime.InteropServices;
using Communication.IO.Tools;
using ECGConversion.ECGDemographics;
using ECGConversion.ECGDiagnostic;

namespace ECGConversion.SCP
{
	/// <summary>
	/// Class contains section 8 (contains the full text ECG interpetive statements section).
	/// </summary>
	public class SCPSection8 : SCPSection, IDiagnostic
	{
		// Defined in SCP.
		private static ushort _SectionID = 8;

		// Part of the stored Data Structure.
		private byte _Confirmed = 0;
		private SCPDate _Date = null;
		private SCPTime _Time = null;
		private byte _NrStatements = 0;
		private SCPStatement[] _Statements = null;
		protected override int _Read(byte[] buffer, int offset)
		{
			int startsize = Marshal.SizeOf(_Confirmed) + SCPDate.Size + SCPTime.Size + Marshal.SizeOf(_NrStatements);
			int end = offset - Size + Length;
			if ((offset + startsize) > end)
			{
				return 0x1;
			}

			_Confirmed = (byte) BytesTool.readBytes(buffer, offset, Marshal.SizeOf(_Confirmed), true);
			offset += Marshal.SizeOf(_Confirmed);
			_Date = new SCPDate();
			_Date.Read(buffer, offset);
			offset += SCPDate.Size;
			_Time = new SCPTime();
			_Time.Read(buffer, offset);
			offset += SCPTime.Size;
			_NrStatements = (byte) BytesTool.readBytes(buffer, offset, Marshal.SizeOf(_NrStatements), true);
			offset += Marshal.SizeOf(_NrStatements);

			if (_NrStatements > 0)
			{
				_Statements = new SCPStatement[_NrStatements];
				int loper=0;
				for (;loper < _NrStatements;loper++)
				{
					_Statements[loper] = new SCPStatement();
					int err = _Statements[loper].Read(buffer, offset);
					if (err != 0)
					{
						return 0x2;
					}
					offset += _Statements[loper].getLength();
				}
				if (loper != _NrStatements)
				{
					_NrStatements = (byte) loper;
					return 0x4;
				}
			}

			return 0x0;
		}
		protected override int _Write(byte[] buffer, int offset)
		{
			BytesTool.writeBytes(_Confirmed, buffer, offset, Marshal.SizeOf(_Confirmed), true);
			offset += Marshal.SizeOf(_Confirmed);
			_Date.Write(buffer, offset);
			offset += SCPDate.Size;
			_Time.Write(buffer, offset);
			offset += SCPTime.Size;
			BytesTool.writeBytes(_NrStatements, buffer, offset, Marshal.SizeOf(_NrStatements), true);
			offset += Marshal.SizeOf(_NrStatements);
			for (int loper=0;loper < _NrStatements;loper++)
			{
				_Statements[loper].Write(buffer, offset);
				offset += _Statements[loper].getLength();
			}
			return 0x0;
		}
		protected override void _Empty()
		{
			_Confirmed = 0;
			_Date = null;
			_Time = null;
			_NrStatements = 0;
			_Statements = null;
		}
		protected override int _getLength()
		{
			if (Works())
			{
				int sum = Marshal.SizeOf(_Confirmed) + SCPDate.Size + SCPTime.Size + Marshal.SizeOf(_NrStatements);
				for (int loper=0;loper < _NrStatements;loper++)
				{
					sum += _Statements[loper].getLength();
				}
				return ((sum % 2) == 0 ? sum : sum + 1);
			}
			return 0;
		}
		public override ushort getSectionID()
		{
			return _SectionID;
		}
		public override bool Works()
		{
			if ((_Date != null)
			&&	(_Time != null)
			&&  ((_NrStatements == 0)
			||	 ((_Statements != null)
			&&	  (_NrStatements <= _Statements.Length))))
			{
				for (int loper=0;loper < _NrStatements;loper++)
				{
					if (_Statements[loper] == null)
					{
						return false;
					}
				}
				return true;
			}
			return false;
		}
		// getting diagnositc information.
		public int getDiagnosticStatements(out Statements stat)
		{
			stat = null;
			if ((_Date != null)
			&&	(_Time != null)
			&&	(_NrStatements > 0))
			{
				stat = new Statements();
				stat.confirmed = (_Confirmed == 1);
				
				if ((_Date.Year != 0)
				&&	(_Date.Month != 0)
				&&	(_Date.Day != 0))
					stat.time = new DateTime(_Date.Year, _Date.Month, _Date.Day, _Time.Hour, _Time.Min, _Time.Sec);
				else
					stat.time = DateTime.MinValue;
				
				stat.statement = new string[_NrStatements];
				for (int loper=0;loper < _NrStatements;loper++)
				{
					if ((_Statements[loper] != null)
					&&  (_Statements[loper].Field != null)
					&&  (_Statements[loper].Length <= _Statements[loper].Field.Length))
					{
						stat.statement[loper] = BytesTool.readString(_Encoding, _Statements[loper].Field, 0, _Statements[loper].Length);
					}
				}

				if  ((stat.statement.Length == 1)
				&&   ((stat.statement[0] == null)
				||	  (stat.statement[0].Length == 0)))
				{
					stat = null;

					return 1;
				}

				return 0;
			}
			return 1;
		}
		// setting diagnositc information.
		public int setDiagnosticStatements(Statements stat)
		{
			if ((stat != null)
			&&  (stat.time.Year > 1000)
			&&  (stat.statement != null)
			&&  (stat.statement.Length > 0))
			{
				Empty();
				_Confirmed = (byte) (stat.confirmed ? 1 : 0);
				
				if (stat.time == DateTime.MinValue)
				{
					_Date = new SCPDate();
					_Time = new SCPTime();
				}
				else
				{
					_Date = new SCPDate(stat.time.Year, stat.time.Month, stat.time.Day);
					_Time = new SCPTime(stat.time.Hour, stat.time.Minute, stat.time.Second);
				}
				
				_NrStatements = (byte) stat.statement.Length;
				_Statements = new SCPStatement[_NrStatements];
				for (int loper=0;loper < _NrStatements;loper++)
				{
					_Statements[loper] = new SCPStatement();
					_Statements[loper].SequenceNr = (byte) (loper + 1);
					if (stat.statement[loper] != null)
					{
						_Statements[loper].Length = (ushort) (stat.statement[loper].Length + 1);
						_Statements[loper].Field = new byte[_Statements[loper].Length];
						BytesTool.writeString(_Encoding, stat.statement[loper], _Statements[loper].Field, 0, _Statements[loper].Length);
					}
					else
					{
						_Statements[loper].Length = 1;
						_Statements[loper].Field = new byte[_Statements[loper].Length];
					}
				}
				return 0;
			}
			return 1;
		}
		/// <summary>
		/// Class containing SCP diagnostic statement.
		/// </summary>
		public class SCPStatement
		{
			public byte SequenceNr;
			public ushort Length;
			public byte[] Field;
			/// <summary>
			/// Constructor for an SCP statement.
			/// </summary>
			public SCPStatement()
			{}
			/// <summary>
			/// Constructor for an SCP statement.
			/// </summary>
			/// <param name="seqnr">sequence number</param>
			/// <param name="length">length of field</param>
			/// <param name="field">field to use</param>
			public SCPStatement(byte seqnr, ushort length, byte[] field)
			{
				SequenceNr = seqnr;
				Length = length;
				Field = field;
			}
			/// <summary>
			/// Function to read SCP statement.
			/// </summary>
			/// <param name="buffer">byte array to read from</param>
			/// <param name="offset">position to start reading</param>
			/// <returns>0 on success</returns>
			public int Read(byte[] buffer, int offset)
			{
				if ((offset + Marshal.SizeOf(SequenceNr) + Marshal.SizeOf(Length)) > buffer.Length)
				{
					return 0x1;
				}

				SequenceNr = (byte) BytesTool.readBytes(buffer, offset, Marshal.SizeOf(SequenceNr), true);
				offset += Marshal.SizeOf(SequenceNr);
				Length = (ushort) BytesTool.readBytes(buffer, offset, Marshal.SizeOf(Length), true);
				offset += Marshal.SizeOf(Length);

				if ((offset + Length) > buffer.Length)
				{
					return 0x2;
				}

				if (Length > 0)
				{
					Field = new byte[Length];
					offset += BytesTool.copy(Field, 0, buffer, offset, Length);
				}

				return 0x0;
			}
			/// <summary>
			/// Function to write SCP statement.
			/// </summary>
			/// <param name="buffer">byte array to write into</param>
			/// <param name="offset">position to start writing</param>
			/// <returns>0 on success</returns>
			public int Write(byte[] buffer, int offset)
			{
				if ((Field == null)
				||  (Field.Length != Length))
				{
					return 0x1;
				}

				if ((offset + Marshal.SizeOf(SequenceNr) + Marshal.SizeOf(Length)) > buffer.Length)
				{
					return 0x2;
				}

				BytesTool.writeBytes(SequenceNr, buffer, offset, Marshal.SizeOf(SequenceNr), true);
				offset += Marshal.SizeOf(SequenceNr);
				BytesTool.writeBytes(Length, buffer, offset, Marshal.SizeOf(Length), true);
				offset += Marshal.SizeOf(Length);

				if ((offset + Length) > buffer.Length)
				{
					return 0x2;
				}

				if (Length > 0)
				{
					offset += BytesTool.copy(buffer, offset, Field, 0, Length);
				}

				return 0x0;
			}
			/// <summary>
			/// Function to get length of SCP statement in bytes.
			/// </summary>
			/// <returns>length of scp statements</returns>
			public int getLength()
			{
				int sum = Marshal.SizeOf(SequenceNr) + Marshal.SizeOf(Length);
				if ((Length > 0)
				&&	(Field != null)
				&&  (Length == Field.Length))
				{
					sum += Length;
				}
				return sum;
			}
		}
	}
}
