// Copyright 2013 Zynga Inc.
//	
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//		
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.
#if !DYNAMIC_SUPPORT

using System;
using System.Collections;
using System.Collections.Generic;
using PlayScript;
using PlayScript.Expando;

namespace PlayScript.DynamicRuntime
{
	public class PSSetIndex
	{
		private PSSetMember	mSetMember;
		private string		mPreviousString;
		private int			mHintDictionaryStringID = -1;
		private int			mHintDictionaryIndex = -1;

		public T SetIndexAs<T> (object o, int index, T value)
		{
			Stats.Increment(StatsCounter.SetIndexBinderInvoked);
			Stats.Increment(StatsCounter.SetIndexBinder_Int_Invoked);

			var l = o as IList<T>;
			if (l != null) {
				l [index] = value;
				return value;
			} 


			var l2 = o as IList;
			if (l2 != null) {
				l2 [index] = value;
				return value;
			} 

			var d = o as IDictionary<int,T>;
			if (d != null) {
				d[index] = value;
				return value;
			} 

			var d2 = o as IDictionary;
			if (d2 != null) {
				d2[index] = value;
				return value;
			}

			return default(T);
		}

		public T SetIndexAs<T> (object o, uint index, T value)
		{
			return SetIndexAs<T>(o, (int)index, value);
		}

		public T SetIndexAs<T> (object o, double index, T value)
		{
			return SetIndexAs<T>(o, (int)index, value);
		}

		public T SetIndexAs<T> (object o, string key, T value)
		{
			Stats.Increment(StatsCounter.SetIndexBinderInvoked);
			Stats.Increment(StatsCounter.SetIndexBinder_Key_Invoked);

			// First let's look if we can cast to a specific primitive
			IFastDictionaryLookup<T> fastLookupT = o as IFastDictionaryLookup<T>;
			if (fastLookupT != null)
			{
				if (key != mPreviousString)
				{
					mHintDictionaryStringID = -1;
					mHintDictionaryIndex = -1;
					mPreviousString = key;
				}

				fastLookupT.SetValue(key, value, ref mHintDictionaryStringID, ref mHintDictionaryIndex);
				return value;
			}

			// If not, T is not a primitive (or object), thus it is probably a class, so use the IFastDictionaryLookup<object> version.
			IFastDictionaryLookup<object> fastLookupObject = o as IFastDictionaryLookup<object>;
			if (fastLookupObject != null)
			{
				if (key != mPreviousString)
				{
					mHintDictionaryStringID = -1;
					mHintDictionaryIndex = -1;
					mPreviousString = key;
				}

				fastLookupObject.SetValue(key, value, ref mHintDictionaryStringID, ref mHintDictionaryIndex);
				return value;
			}

			// handle dictionaries
			var dict = o as IDictionary;
			if (dict != null) {
				Stats.Increment(StatsCounter.SetIndexBinder_Key_Dictionary_Invoked);

				dict[key] = (object)value;
				return value;
			} 

			// fallback on setmemberbinder to do the hard work 
			Stats.Increment(StatsCounter.SetIndexBinder_Key_Property_Invoked);

			// create a set member binder here to set
			if (mSetMember == null) {
				mSetMember = new PSSetMember(key);
			}

			// set member value
			mSetMember.SetNamedMember(o, key, value);	
			return value;
		}

		public T SetIndexAs<T> (object o, object key, T value)
		{
			if (key is int) {
				SetIndexAs<T>(o, (int)key, value);
			} else if (key is string) {
				SetIndexAs<T>(o, (string)key, value);
			} else  if (key is uint) {
				SetIndexAs<T>(o, (uint)key, value);
			} else  if (key is double) {
				SetIndexAs<T>(o, (double)key, value);
			} else {
				throw new InvalidOperationException("Cannot index object with key of type: " + key.GetType());
			}
			return value;
		}

		public PSSetIndex()
		{
		}
	}
}
#endif

